using System.Globalization;
using System.Linq.Expressions;
using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Templates.Models;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Templates;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Fsms.Templates.Queries.GetTemplates;

[Authorize(Policy = FsmsPolicies.ViewTemplates)]
public record GetTemplatesQuery : IRequest<Result<PagedResult<TemplateListItemDto>>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Category { get; init; }
    public string? Status { get; init; }
    public int? DepartmentId { get; init; }
    public string? BranchCode { get; init; }
    public string? Search { get; init; }
}

public sealed class GetTemplatesQueryValidator : AbstractValidator<GetTemplatesQuery>
{
    public GetTemplatesQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 1000).WithMessage("Page size must be between 1 and 1000.");
    }
}

/// <summary>
/// The back-office template grid, narrowed to the caller's territory. Every status is listed — that
/// is what separates this from the field crew's <c>GetAvailableTemplatesForFieldTeamQuery</c> — but
/// territory is enforced here too: a scoped operator has no business raising work on a form that
/// belongs to another operation area.
/// </summary>
public sealed class GetTemplatesQueryHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetTemplatesQuery, Result<PagedResult<TemplateListItemDto>>>
{
    /// <summary>The grid's columns only — the definition JSON never rides along on a list read.</summary>
    private sealed record TemplateRow(
        long Id,
        string TemplateCode,
        string TemplateNameEn,
        string TemplateNameAr,
        string Category,
        string Status,
        int? DepartmentId,
        string? BranchScope,
        string? FaTypeCode,
        int TeamFillSlaHours,
        int CompletionSlaHours,
        bool AutoMigrateSurveysOnPublish,
        int? CurrentVersionNo,
        DateTimeOffset Created,
        DateTimeOffset LastModified);

    public async Task<Result<PagedResult<TemplateListItemDto>>> Handle(
        GetTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.SurveyTemplates.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var category = request.Category.Trim();
            query = query.Where(x => x.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(x => x.Status == status);
        }

        if (request.DepartmentId is int departmentId)
        {
            query = query.Where(x => x.DepartmentId == departmentId);
        }

        if (!string.IsNullOrWhiteSpace(request.BranchCode))
        {
            var branchCode = request.BranchCode.Trim();
            query = query.Where(x => x.BranchScope != null && x.BranchScope.Contains(branchCode));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                x.TemplateCode.Contains(search) ||
                x.TemplateNameEn.Contains(search) ||
                x.TemplateNameAr.Contains(search));
        }

        var ordered = query.OrderBy(x => x.TemplateCode).Select(ToRow());
        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);

        if (callerScope.IsUnrestricted)
        {
            var totalCount = await query.CountAsync(cancellationToken);

            var page = await ordered
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var scopes = await LoadScopeAssignmentsAsync(page, cancellationToken);

            return Paged(page, scopes, totalCount, request);
        }

        // A template's territory lives in ORG_SCOPES rather than on a column this query can filter
        // in SQL, so the scope test happens in memory. Paging first would hand back short pages with
        // matches still unseen behind them, so every candidate is loaded, filtered, then paged.
        // Templates number in the hundreds, which is what makes that affordable.
        var candidates = await ordered.ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return Paged([], new Dictionary<string, IReadOnlyList<OrgScopeAssignment>>(), 0, request);
        }

        var assignmentsByTemplateId = await LoadScopeAssignmentsAsync(candidates, cancellationToken);
        var hierarchy = await OrgHierarchyCache.GetAsync(context, cache, cacheSettings.Value, cancellationToken);

        // Visibility is the "do we share any ground" question, so Overlaps rather than Covers. A
        // template with no scope rows expands to Unrestricted and stays visible to everyone, which
        // is the same rule OrgScopeSet applies to every other owner.
        var visible = candidates
            .Where(x => callerScope.Overlaps(OrgScopeSet.FromAssignments(
                assignmentsByTemplateId.GetValueOrDefault(Key(x.Id), []), hierarchy)))
            .ToList();

        var items = visible
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Paged(items, assignmentsByTemplateId, visible.Count, request);
    }

    /// <summary>
    /// The scope rows behind a set of templates, read once. They are needed twice — to decide what
    /// the caller may see, and to fill each row's <see cref="TemplateListItemDto.Scopes"/> — which is
    /// why this reads <c>ORG_SCOPES</c> directly rather than going through
    /// <see cref="IOrgScopeService.GetScopesAsync"/>, whose expanded sets cannot be shown as-is.
    /// </summary>
    private async Task<Dictionary<string, IReadOnlyList<OrgScopeAssignment>>> LoadScopeAssignmentsAsync(
        IReadOnlyCollection<TemplateRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var templateIds = rows.Select(x => Key(x.Id)).ToList();

        var scopeRows = await context.OrgScopes
            .AsNoTracking()
            .Where(x => x.OwnerType == OrgScopeOwnerTypes.Template && x.IsActive && templateIds.Contains(x.OwnerId))
            .Select(x => new { x.OwnerId, x.Level, x.Code, x.DepartmentId })
            .ToListAsync(cancellationToken);

        return scopeRows
            .GroupBy(x => x.OwnerId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<OrgScopeAssignment>)g
                    .Select(x => new OrgScopeAssignment { Level = x.Level, Code = x.Code, DepartmentId = x.DepartmentId })
                    .ToList());
    }

    private static Result<PagedResult<TemplateListItemDto>> Paged(
        IReadOnlyList<TemplateRow> rows,
        IReadOnlyDictionary<string, IReadOnlyList<OrgScopeAssignment>> assignmentsByTemplateId,
        int totalCount,
        GetTemplatesQuery request)
    {
        var items = rows
            .Select(x => new TemplateListItemDto
            {
                TemplateId = x.Id,
                TemplateCode = x.TemplateCode,
                TemplateNameEn = x.TemplateNameEn,
                TemplateNameAr = x.TemplateNameAr,
                Category = x.Category,
                Status = x.Status,
                DepartmentId = x.DepartmentId,
                BranchScope = x.BranchScope,
                FaTypeCode = x.FaTypeCode,
                TeamFillSlaHours = x.TeamFillSlaHours,
                CompletionSlaHours = x.CompletionSlaHours,
                AutoMigrateSurveysOnPublish = x.AutoMigrateSurveysOnPublish,
                CurrentVersionNo = x.CurrentVersionNo,
                Created = x.Created,
                LastModified = x.LastModified,
                Scopes = assignmentsByTemplateId.GetValueOrDefault(Key(x.Id), [])
            })
            .ToList();

        return Result<PagedResult<TemplateListItemDto>>.Success(
            new PagedResult<TemplateListItemDto>(items, totalCount, request.Page, request.PageSize));
    }

    /// <summary><c>ORG_SCOPES.OwnerId</c> is a string for every owner type, templates included.</summary>
    private static string Key(long templateId) => templateId.ToString(CultureInfo.InvariantCulture);

    private static Expression<Func<SurveyTemplate, TemplateRow>> ToRow() =>
        x => new TemplateRow(
            x.Id,
            x.TemplateCode,
            x.TemplateNameEn,
            x.TemplateNameAr,
            x.Category,
            x.Status,
            x.DepartmentId,
            x.BranchScope,
            x.FaTypeCode,
            x.TeamFillSlaHours,
            x.CompletionSlaHours,
            x.AutoMigrateSurveysOnPublish,
            x.CurrentVersionNo,
            x.Created,
            x.LastModified);
}
