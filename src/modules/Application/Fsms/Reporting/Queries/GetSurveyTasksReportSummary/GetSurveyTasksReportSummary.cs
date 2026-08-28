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

namespace KH.Application.Fsms.Reporting.Queries.GetSurveyTasksReportSummary;

/// <summary>
/// The "Survey Tasks" report's summary screen: KPIs, the daily creation trend, the status
/// doughnut, task counts by team and by branch, and the worklist of tasks that missed a deadline —
/// all read off the same filtered survey set in one round trip, per the report's "don't call a
/// separate API per KPI" rule. The paginated row-by-row table is a second query
/// (<c>GetSurveyTasksReportDetailsQuery</c>) so a KPI refresh
/// never pays for fetching every row.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewReports)]
public record GetSurveyTasksReportSummaryQuery : IRequest<Result<SurveyTasksReportSummaryDto>>
{
    /// <summary>Inclusive lower bound on <see cref="Survey.Created"/>.</summary>
    public DateTimeOffset? FromDate { get; init; }

    /// <summary>Inclusive upper bound on <see cref="Survey.Created"/>.</summary>
    public DateTimeOffset? ToDate { get; init; }

    /// <summary>See <see cref="SurveyStatuses"/>.</summary>
    public string? Status { get; init; }

    /// <summary>
    /// Narrows to the team holding a survey's most recent assignment — not necessarily the active
    /// one, since an approved survey's allocation is no longer active.
    /// </summary>
    public long? TeamId { get; init; }

    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }

    /// <summary>See <see cref="SurveySources"/>.</summary>
    public string? Source { get; init; }
}

public sealed class GetSurveyTasksReportSummaryQueryValidator : AbstractValidator<GetSurveyTasksReportSummaryQuery>
{
    public GetSurveyTasksReportSummaryQueryValidator()
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

public sealed class GetSurveyTasksReportSummaryQueryHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetSurveyTasksReportSummaryQuery, Result<SurveyTasksReportSummaryDto>>
{
    /// <summary>
    /// How many candidate rows each of the two lateness queries pulls before the exact "days late"
    /// is computed and the set is trimmed to <see cref="LateTasksLimit"/>. Bounds the read regardless
    /// of how wide the caller's date range is, without needing day-level arithmetic translated to SQL.
    /// </summary>
    private const int LateCandidateBuffer = 200;

    private const int LateTasksLimit = 20;

    public async Task<Result<SurveyTasksReportSummaryDto>> Handle(
        GetSurveyTasksReportSummaryQuery request, CancellationToken cancellationToken)
    {
        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);
        var filtered = BuildFilteredQuery(context, request, callerScope);
        var now = DateTimeOffset.UtcNow;

        var kpis = await BuildKpisAsync(filtered, now, cancellationToken);
        var dailyTrend = await BuildDailyTrendAsync(filtered, cancellationToken);
        var statusDistribution = BuildStatusDistribution(kpis.StatusCountMap, kpis.TotalTasks);
        var tasksByTeam = await BuildTasksByTeamAsync(context, filtered, cancellationToken);
        var tasksByBranch = await BuildTasksByBranchAsync(context, cache, cacheSettings.Value, filtered, cancellationToken);
        var lateTasks = await BuildLateTasksAsync(context, filtered, now, cancellationToken);

        return Result<SurveyTasksReportSummaryDto>.Success(new SurveyTasksReportSummaryDto
        {
            Kpis = kpis.Dto,
            DailyTrend = dailyTrend,
            StatusDistribution = statusDistribution,
            TasksByTeam = tasksByTeam,
            TasksByBranch = tasksByBranch,
            LateTasks = lateTasks,
        });
    }

    /// <summary>
    /// The filtered, org-scoped survey set every widget on the summary reads from. Built once and
    /// reused as an <see cref="IQueryable{T}"/> across several independent aggregate queries, rather
    /// than materialized here, so each widget's query stays server-side.
    /// </summary>
    private static IQueryable<Survey> BuildFilteredQuery(
        IApplicationDbContext context,
        GetSurveyTasksReportSummaryQuery request,
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
            // The team for a survey is its most recent assignment row, not necessarily the active
            // one — an approved survey's allocation is no longer active.
            query = query.Where(x => x.Assignments
                .OrderByDescending(a => a.AssignedDate)
                .ThenByDescending(a => a.Id)
                .Select(a => (long?)a.FieldTeamId)
                .FirstOrDefault() == teamId);
        }

        return query;
    }

    private static async Task<SurveyTasksKpiResult> BuildKpisAsync(
        IQueryable<Survey> filtered, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var statusCounts = await filtered
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var statusCountMap = statusCounts.ToDictionary(x => x.Status, x => x.Count, StringComparer.Ordinal);
        var totalTasks = statusCountMap.Values.Sum();
        var approvedTasks = statusCountMap.GetValueOrDefault(SurveyStatuses.Approved);

        var overdueTasks = await filtered
            .Where(x => x.Status != SurveyStatuses.Approved && x.Status != SurveyStatuses.Expired)
            .Where(x => x.DueDate != null && x.DueDate < now)
            .CountAsync(cancellationToken);

        var dto = new SurveyTasksReportKpisDto
        {
            TotalTasks = totalTasks,
            PendingTasks = statusCountMap.GetValueOrDefault(SurveyStatuses.Created)
                + statusCountMap.GetValueOrDefault(SurveyStatuses.Assigned),
            InProgressTasks = statusCountMap.GetValueOrDefault(SurveyStatuses.InProgress),
            SubmittedTasks = statusCountMap.GetValueOrDefault(SurveyStatuses.Submitted),
            ApprovedTasks = approvedTasks,
            ReturnedTasks = statusCountMap.GetValueOrDefault(SurveyStatuses.Returned),
            OverdueTasks = overdueTasks,
            CompletionRatePercent = totalTasks == 0 ? 0m : Math.Round(approvedTasks * 100m / totalTasks, 2),
        };

        return new SurveyTasksKpiResult(dto, statusCountMap, totalTasks);
    }

    private static IReadOnlyList<SurveyTaskStatusSliceDto> BuildStatusDistribution(
        IReadOnlyDictionary<string, int> statusCountMap, int totalTasks) =>
        [.. SurveyStatuses.All.Select(status =>
        {
            var count = statusCountMap.GetValueOrDefault(status);
            return new SurveyTaskStatusSliceDto
            {
                Status = status,
                Count = count,
                Percent = totalTasks == 0 ? 0m : Math.Round(count * 100m / totalTasks, 2),
            };
        })];

    private static async Task<IReadOnlyList<SurveyTasksDailyTrendPointDto>> BuildDailyTrendAsync(
        IQueryable<Survey> filtered, CancellationToken cancellationToken)
    {
        var rows = await filtered
            .GroupBy(x => x.Created.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(x => new SurveyTasksDailyTrendPointDto
        {
            Date = DateOnly.FromDateTime(x.Date),
            CreatedCount = x.Count,
        })];
    }

    private static async Task<IReadOnlyList<SurveyTasksByTeamDto>> BuildTasksByTeamAsync(
        IApplicationDbContext context, IQueryable<Survey> filtered, CancellationToken cancellationToken)
    {
        var rows = await filtered
            .Select(x => new
            {
                TeamId = x.Assignments
                    .OrderByDescending(a => a.AssignedDate)
                    .ThenByDescending(a => a.Id)
                    .Select(a => (long?)a.FieldTeamId)
                    .FirstOrDefault(),
            })
            .GroupBy(x => x.TeamId)
            .Select(g => new { TeamId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        var teamIds = rows.Where(x => x.TeamId.HasValue).Select(x => x.TeamId!.Value).Distinct().ToList();

        Dictionary<long, string> teamNames = teamIds.Count == 0
            ? []
            : await context.FieldTeams
                .AsNoTracking()
                .Where(x => teamIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return [.. rows.Select(x => new SurveyTasksByTeamDto
        {
            TeamId = x.TeamId,
            TeamName = x.TeamId is long teamId && teamNames.TryGetValue(teamId, out var name)
                ? name
                : ReportUnassignedLabels.Team,
            Count = x.Count,
        })];
    }

    private static async Task<IReadOnlyList<SurveyTasksByBranchDto>> BuildTasksByBranchAsync(
        IApplicationDbContext context,
        ICacheService cache,
        CacheSettings cacheSettings,
        IQueryable<Survey> filtered,
        CancellationToken cancellationToken)
    {
        var rows = await filtered
            .GroupBy(x => x.BranchCode)
            .Select(g => new { BranchCode = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        var branchNames = await LookupNameCache.GetBranchNamesAsync(context, cache, cacheSettings, cancellationToken);

        return [.. rows.Select(x => new SurveyTasksByBranchDto
        {
            BranchCode = x.BranchCode,
            BranchName = x.BranchCode is null
                ? ReportUnassignedLabels.Branch
                : branchNames.FindBranch(x.BranchCode)?.NameEn ?? x.BranchCode,
            Count = x.Count,
        })];
    }

    private static async Task<IReadOnlyList<SurveyTaskLateItemDto>> BuildLateTasksAsync(
        IApplicationDbContext context, IQueryable<Survey> filtered, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var overdueRows = await filtered
            .Where(x => x.Status != SurveyStatuses.Approved && x.Status != SurveyStatuses.Expired)
            .Where(x => x.DueDate != null && x.DueDate < now)
            .OrderBy(x => x.DueDate)
            .Take(LateCandidateBuffer)
            .Select(x => new SurveyTaskLateCandidateRow
            {
                SurveyId = x.Id,
                SurveyCode = x.SurveyCode,
                Status = x.Status,
                LatenessKind = SurveyTaskLatenessKinds.Overdue,
                DueDate = x.DueDate,
                CompletedDate = null,
                TeamId = x.Assignments
                    .OrderByDescending(a => a.AssignedDate)
                    .ThenByDescending(a => a.Id)
                    .Select(a => (long?)a.FieldTeamId)
                    .FirstOrDefault(),
                BranchCode = x.BranchCode,
                TemplateId = x.TemplateId,
            })
            .ToListAsync(cancellationToken);

        var completedLateRows = await filtered
            .Where(x => x.Status == SurveyStatuses.Approved)
            .Where(x => x.DueDate != null && x.CompletedDate != null && x.CompletedDate > x.DueDate)
            .OrderByDescending(x => x.CompletedDate)
            .Take(LateCandidateBuffer)
            .Select(x => new SurveyTaskLateCandidateRow
            {
                SurveyId = x.Id,
                SurveyCode = x.SurveyCode,
                Status = x.Status,
                LatenessKind = SurveyTaskLatenessKinds.CompletedLate,
                DueDate = x.DueDate,
                CompletedDate = x.CompletedDate,
                TeamId = x.Assignments
                    .OrderByDescending(a => a.AssignedDate)
                    .ThenByDescending(a => a.Id)
                    .Select(a => (long?)a.FieldTeamId)
                    .FirstOrDefault(),
                BranchCode = x.BranchCode,
                TemplateId = x.TemplateId,
            })
            .ToListAsync(cancellationToken);

        var candidates = overdueRows.Concat(completedLateRows).ToList();

        if (candidates.Count == 0)
        {
            return [];
        }

        var templateIds = candidates.Select(x => x.TemplateId).Distinct().ToList();
        var templateNames = await context.SurveyTemplates
            .AsNoTracking()
            .Where(x => templateIds.Contains(x.Id))
            .Select(x => new { x.Id, x.TemplateNameEn })
            .ToDictionaryAsync(x => x.Id, x => x.TemplateNameEn, cancellationToken);

        var teamIds = candidates.Where(x => x.TeamId.HasValue).Select(x => x.TeamId!.Value).Distinct().ToList();
        Dictionary<long, string> teamNames = teamIds.Count == 0
            ? []
            : await context.FieldTeams
                .AsNoTracking()
                .Where(x => teamIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return [.. candidates
            .Select(row =>
            {
                var daysLate = row.LatenessKind == SurveyTaskLatenessKinds.Overdue
                    ? (int)Math.Max(0, (now - row.DueDate!.Value).TotalDays)
                    : (int)Math.Max(0, (row.CompletedDate!.Value - row.DueDate!.Value).TotalDays);

                return new SurveyTaskLateItemDto
                {
                    SurveyId = row.SurveyId,
                    SurveyCode = row.SurveyCode,
                    Status = row.Status,
                    LatenessKind = row.LatenessKind,
                    DaysLate = daysLate,
                    DueDate = row.DueDate,
                    TeamId = row.TeamId,
                    TeamName = row.TeamId is long teamId && teamNames.TryGetValue(teamId, out var teamName)
                        ? teamName
                        : null,
                    BranchCode = row.BranchCode,
                    TemplateNameEn = templateNames.GetValueOrDefault(row.TemplateId),
                };
            })
            .OrderByDescending(x => x.DaysLate)
            .Take(LateTasksLimit)];
    }

    private sealed record SurveyTasksKpiResult(
        SurveyTasksReportKpisDto Dto, IReadOnlyDictionary<string, int> StatusCountMap, int TotalTasks);
}

/// <summary>
/// A late-or-overdue survey before its "days late" and its team/template labels are resolved.
/// Internal rather than nested and private: EF materializes a projection through generated
/// accessors, and a private nested target compiles cleanly and then fails at runtime.
/// </summary>
internal sealed class SurveyTaskLateCandidateRow
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string LatenessKind { get; init; } = default!;
    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? CompletedDate { get; init; }
    public long? TeamId { get; init; }
    public string? BranchCode { get; init; }
    public long TemplateId { get; init; }
}
