using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Lookups;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Reporting.Models;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Surveys;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Fsms.Reporting.Queries.GetSurveyTasksReportExportRows;

/// <summary>
/// The full filtered row set behind the "Survey Tasks" detail table, for Excel/PDF export — as
/// opposed to <c>GetSurveyTasksReportDetailsQuery</c>, whose <c>PageSize</c> is capped at 100 for the
/// UI table. An export must reflect every row the caller's filters match (spec: exports operate on
/// the full filtered dataset, not just the page on screen), so this is a separate, unpaged query
/// rather than a loosened cap on the paginated one — the two have different callers and different
/// safety requirements.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewReports)]
public record GetSurveyTasksReportExportRowsQuery : IRequest<Result<IReadOnlyList<SurveyTaskReportRowDto>>>
{
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
    public string? Status { get; init; }
    public long? TeamId { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public string? Source { get; init; }
}

public sealed class GetSurveyTasksReportExportRowsQueryValidator : AbstractValidator<GetSurveyTasksReportExportRowsQuery>
{
    public GetSurveyTasksReportExportRowsQueryValidator()
    {
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value)
            .WithMessage("The end of the date range must not precede its start.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);

        RuleFor(x => x.Status)
            .Must(SurveyStatuses.IsDefined).WithMessage("Status is not a recognized survey status.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));

        RuleFor(x => x.Source)
            .Must(SurveySources.IsDefined).WithMessage("Source is not a recognized survey source.")
            .When(x => !string.IsNullOrWhiteSpace(x.Source));

        RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("Team id must be greater than 0.")
            .When(x => x.TeamId.HasValue);
    }
}

public sealed class GetSurveyTasksReportExportRowsQueryHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetSurveyTasksReportExportRowsQuery, Result<IReadOnlyList<SurveyTaskReportRowDto>>>
{
    /// <summary>
    /// Hard ceiling on an export's row count, independent of how wide the caller's filters are.
    /// Protects memory and response size; a caller wanting more should narrow the date range.
    /// </summary>
    private const int MaxExportRows = 20_000;

    public async Task<Result<IReadOnlyList<SurveyTaskReportRowDto>>> Handle(
        GetSurveyTasksReportExportRowsQuery request, CancellationToken cancellationToken)
    {
        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);
        var filtered = BuildFilteredQuery(context, request, callerScope);

        var rows = await filtered
            .OrderByDescending(x => x.Created)
            .ThenByDescending(x => x.Id)
            .Take(MaxExportRows)
            .Select(x => new SurveyTaskExportRow
            {
                SurveyId = x.Id,
                SurveyCode = x.SurveyCode,
                TemplateId = x.TemplateId,
                TemplateVersionNo = x.TemplateVersionNo,
                Status = x.Status,
                Source = x.Source,
                BranchCode = x.BranchCode,
                DepartmentId = x.DepartmentId,
                TeamId = x.Assignments
                    .OrderByDescending(a => a.AssignedDate)
                    .ThenByDescending(a => a.Id)
                    .Select(a => (long?)a.FieldTeamId)
                    .FirstOrDefault(),
                CustomerName = x.CustomerName,
                FaId = x.FaId,
                Created = x.Created,
                DueDate = x.DueDate,
                SubmissionCount = x.SubmissionCount,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Result<IReadOnlyList<SurveyTaskReportRowDto>>.Success([]);
        }

        var branchNames = await LookupNameCache.GetBranchNamesAsync(context, cache, cacheSettings.Value, cancellationToken);

        var templateIds = rows.Select(x => x.TemplateId).Distinct().ToList();
        var templates = await context.SurveyTemplates
            .AsNoTracking()
            .Where(x => templateIds.Contains(x.Id))
            .Select(x => new { x.Id, x.TemplateNameEn, x.TemplateNameAr })
            .ToDictionaryAsync(x => x.Id, x => (x.TemplateNameEn, x.TemplateNameAr), cancellationToken);

        var teamIds = rows.Where(x => x.TeamId.HasValue).Select(x => x.TeamId!.Value).Distinct().ToList();
        Dictionary<long, string> teamNames = teamIds.Count == 0
            ? []
            : await context.FieldTeams
                .AsNoTracking()
                .Where(x => teamIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var items = rows
            .Select(row =>
            {
                var branch = branchNames.FindBranch(row.BranchCode);
                var hasTemplate = templates.TryGetValue(row.TemplateId, out var template);

                return new SurveyTaskReportRowDto
                {
                    SurveyId = row.SurveyId,
                    SurveyCode = row.SurveyCode,
                    TemplateNameEn = hasTemplate ? template.TemplateNameEn : null,
                    TemplateNameAr = hasTemplate ? template.TemplateNameAr : null,
                    TemplateVersionNo = row.TemplateVersionNo,
                    Status = row.Status,
                    Source = row.Source,
                    BranchCode = row.BranchCode,
                    BranchNameEn = branch?.NameEn,
                    BranchNameAr = branch?.NameAr,
                    DepartmentId = row.DepartmentId,
                    TeamId = row.TeamId,
                    TeamName = row.TeamId is long teamId && teamNames.TryGetValue(teamId, out var name)
                        ? name
                        : null,
                    CustomerName = row.CustomerName,
                    FaId = row.FaId,
                    Created = row.Created,
                    DueDate = row.DueDate,
                    SubmissionCount = row.SubmissionCount,
                };
            })
            .ToList();

        return Result<IReadOnlyList<SurveyTaskReportRowDto>>.Success(items);
    }

    /// <summary>Same filter set as <c>GetSurveyTasksReportDetailsQuery</c>, deliberately duplicated — see that slice's remarks.</summary>
    private static IQueryable<Survey> BuildFilteredQuery(
        IApplicationDbContext context,
        GetSurveyTasksReportExportRowsQuery request,
        OrgScopeSet callerScope)
    {
        IQueryable<Survey> query = context.Surveys.AsNoTracking();

        if (!callerScope.IsUnrestricted)
        {
            query = query.Where(OrgScopeQueryFilter.ForSurveys(callerScope));
        }

        if (request.FromDate is DateTimeOffset fromDate)
        {
            query = query.Where(x => x.Created >= fromDate);
        }

        if (request.ToDate is DateTimeOffset toDate)
        {
            query = query.Where(x => x.Created <= toDate);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(x => x.Status == request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.BranchCode))
        {
            query = query.Where(x => x.BranchCode == request.BranchCode);
        }

        if (!string.IsNullOrWhiteSpace(request.OperationAreaCode))
        {
            query = query.Where(x => x.OperationAreaCode == request.OperationAreaCode);
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            query = query.Where(x => x.Source == request.Source);
        }

        if (request.TeamId is long teamId)
        {
            query = query.Where(x => x.Assignments
                .OrderByDescending(a => a.AssignedDate)
                .ThenByDescending(a => a.Id)
                .Select(a => (long?)a.FieldTeamId)
                .FirstOrDefault() == teamId);
        }

        return query;
    }
}

/// <summary>
/// The survey columns the export needs, before its labels are resolved. Internal rather than nested
/// and private: EF materializes a projection through generated accessors, and a private nested target
/// compiles cleanly and then fails at runtime.
/// </summary>
internal sealed class SurveyTaskExportRow
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;
    public long TemplateId { get; init; }
    public int? TemplateVersionNo { get; init; }
    public string Status { get; init; } = default!;
    public string Source { get; init; } = default!;
    public string? BranchCode { get; init; }
    public int? DepartmentId { get; init; }
    public long? TeamId { get; init; }
    public string? CustomerName { get; init; }
    public string? FaId { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public int SubmissionCount { get; init; }
}
