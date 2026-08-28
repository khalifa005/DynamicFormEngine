using System.Linq.Expressions;
using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Templates;
using Shared.Core.Common;

namespace KH.Application.Fsms.Templates.Queries.GetAvailableTemplatesForFieldTeam;

/// <summary>
/// The templates a field crew may pick from to start a new fill — published only, and narrowed to
/// the caller's own territory. Deliberately a separate, narrower read from the back-office
/// <c>GetTemplatesQuery</c>, which lists every status and every territory for administration.
/// </summary>
[Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
public record GetAvailableTemplatesForFieldTeamQuery : IRequest<Result<PagedResult<FieldTeamTemplateListItemDto>>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? FaTypeCode { get; init; }
}

public sealed class GetAvailableTemplatesForFieldTeamQueryValidator
    : AbstractValidator<GetAvailableTemplatesForFieldTeamQuery>
{
    public GetAvailableTemplatesForFieldTeamQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 1000).WithMessage("Page size must be between 1 and 1000.");
    }
}

public sealed class FieldTeamTemplateListItemDto
{
    public long TemplateId { get; init; }
    public string TemplateCode { get; init; } = default!;
    public string TemplateNameEn { get; init; } = default!;
    public string TemplateNameAr { get; init; } = default!;
    public string Category { get; init; } = default!;
    public string? FaTypeCode { get; init; }
    public int? CurrentVersionNo { get; init; }

    /// <summary>
    /// The form-builder definition to render, so the app can open a blank fill straight from this
    /// list without a second call per template. Taken from the published <c>Mobile</c> snapshot at
    /// <see cref="CurrentVersionNo"/>; a template published before mobile snapshots existed falls
    /// back to its own working definition.
    /// </summary>
    public string DefinitionJson { get; init; } = EmptyDefinition;

    /// <summary>The version <see cref="DefinitionJson"/> was actually taken from.</summary>
    public int? DefinitionVersionNo { get; init; }

    internal const string EmptyDefinition = "{}";
}

public sealed class GetAvailableTemplatesForFieldTeamQueryHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService)
    : IRequestHandler<GetAvailableTemplatesForFieldTeamQuery, Result<PagedResult<FieldTeamTemplateListItemDto>>>
{
    public async Task<Result<PagedResult<FieldTeamTemplateListItemDto>>> Handle(
        GetAvailableTemplatesForFieldTeamQuery request, CancellationToken cancellationToken)
    {
        var query = context.SurveyTemplates
            .AsNoTracking()
            .Where(x => x.Status == TemplateStatuses.Published);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                x.TemplateCode.Contains(search) ||
                x.TemplateNameEn.Contains(search) ||
                x.TemplateNameAr.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(request.FaTypeCode))
        {
            var faTypeCode = request.FaTypeCode.Trim();
            query = query.Where(x => x.FaTypeCode == faTypeCode);
        }

        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);

        if (callerScope.IsUnrestricted)
        {
            var totalCount = await query.CountAsync(cancellationToken);

            var pagedRows = await query
                .OrderBy(x => x.TemplateCode)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(ToRow())
                .ToListAsync(cancellationToken);

            return Result<PagedResult<FieldTeamTemplateListItemDto>>.Success(
                new PagedResult<FieldTeamTemplateListItemDto>(
                    await ToListItemsAsync(pagedRows, cancellationToken),
                    totalCount,
                    request.Page,
                    request.PageSize));
        }

        // The caller has real territory limits, and a template's own territory lives in ORG_SCOPES
        // rather than on a column this query can filter in SQL. Candidate volume is the same order
        // of magnitude as the back-office template list (a few hundred rows at most), so it is
        // cheaper to load every matching candidate once, filter by scope in memory, and page the
        // filtered list — rather than paging first and risking a short page with more results left
        // unseen behind it.
        var candidates = await query
            .OrderBy(x => x.TemplateCode)
            .Select(ToRow())
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return Result<PagedResult<FieldTeamTemplateListItemDto>>.Success(
                new PagedResult<FieldTeamTemplateListItemDto>([], 0, request.Page, request.PageSize));
        }

        var candidateIds = candidates.Select(x => x.TemplateId.ToString()).ToList();
        var scopesByTemplateId = await orgScopeService.GetScopesAsync(
            OrgScopeOwnerTypes.Template, candidateIds, cancellationToken);

        // A template with no scope rows at all is visible to everyone — the same "no rows = open"
        // rule OrgScopeSet already applies to a single owner's own coverage.
        var visible = candidates
            .Where(x => !scopesByTemplateId.TryGetValue(x.TemplateId.ToString(), out var templateScope)
                        || callerScope.Overlaps(templateScope))
            .ToList();

        var items = visible
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result<PagedResult<FieldTeamTemplateListItemDto>>.Success(
            new PagedResult<FieldTeamTemplateListItemDto>(
                await ToListItemsAsync(items, cancellationToken),
                visible.Count,
                request.Page,
                request.PageSize));
    }

    /// <summary>
    /// Attaches each row's form definition. Deliberately run after paging and scope filtering: the
    /// definitions are by far the largest part of the payload, and the scoped branch above loads
    /// every candidate into memory, so fetching them up front would drag the whole catalogue's JSON
    /// through the database for a page of twenty.
    /// </summary>
    private async Task<List<FieldTeamTemplateListItemDto>> ToListItemsAsync(
        List<FieldTeamTemplateRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var templateIds = rows.Select(x => x.TemplateId).Distinct().ToList();

        // A publish writes one snapshot per target client; Mobile is the shape this caller renders.
        var mobileVersions = await context.TemplateVersions
            .AsNoTracking()
            .Where(v => templateIds.Contains(v.TemplateId) && v.TargetClient == TargetClients.Mobile)
            .Select(v => new { v.TemplateId, v.VersionNo, v.SchemaJson })
            .ToListAsync(cancellationToken);

        var snapshots = mobileVersions
            .GroupBy(v => v.TemplateId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // The template's own working definition, for a published template with no mobile snapshot
        // behind it — a row published before snapshots were written per client.
        var workingDefinitions = await context.SurveyTemplates
            .AsNoTracking()
            .Where(x => templateIds.Contains(x.Id))
            .Select(x => new { x.Id, x.DefinitionJson })
            .ToDictionaryAsync(x => x.Id, x => x.DefinitionJson, cancellationToken);

        return rows
            .Select(row =>
            {
                // Pin to the template's current version where a snapshot for it exists; otherwise the
                // newest mobile snapshot, so a template whose current version was only published for
                // the web builder still renders on a device.
                var snapshot = snapshots.TryGetValue(row.TemplateId, out var versions)
                    ? versions.FirstOrDefault(v => v.VersionNo == row.CurrentVersionNo)
                      ?? versions.OrderByDescending(v => v.VersionNo).FirstOrDefault()
                    : null;

                return new FieldTeamTemplateListItemDto
                {
                    TemplateId = row.TemplateId,
                    TemplateCode = row.TemplateCode,
                    TemplateNameEn = row.TemplateNameEn,
                    TemplateNameAr = row.TemplateNameAr,
                    Category = row.Category,
                    FaTypeCode = row.FaTypeCode,
                    CurrentVersionNo = row.CurrentVersionNo,
                    DefinitionJson = snapshot?.SchemaJson
                        ?? workingDefinitions.GetValueOrDefault(row.TemplateId)
                        ?? FieldTeamTemplateListItemDto.EmptyDefinition,
                    DefinitionVersionNo = snapshot?.VersionNo,
                };
            })
            .ToList();
    }

    private static Expression<Func<SurveyTemplate, FieldTeamTemplateRow>> ToRow() =>
        x => new FieldTeamTemplateRow
        {
            TemplateId = x.Id,
            TemplateCode = x.TemplateCode,
            TemplateNameEn = x.TemplateNameEn,
            TemplateNameAr = x.TemplateNameAr,
            Category = x.Category,
            FaTypeCode = x.FaTypeCode,
            CurrentVersionNo = x.CurrentVersionNo,
        };
}

/// <summary>
/// A candidate template before its definition is attached. Internal rather than nested and private:
/// EF materializes a projection through generated accessors, and a private nested target compiles
/// cleanly and then fails at runtime.
/// </summary>
internal sealed class FieldTeamTemplateRow
{
    public long TemplateId { get; init; }
    public string TemplateCode { get; init; } = default!;
    public string TemplateNameEn { get; init; } = default!;
    public string TemplateNameAr { get; init; } = default!;
    public string Category { get; init; } = default!;
    public string? FaTypeCode { get; init; }
    public int? CurrentVersionNo { get; init; }
}
