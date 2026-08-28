using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Reporting.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Reporting.Queries.GetTeamPerformanceReport;

/// <summary>
/// Team Performance report: KPIs, a per-team breakdown and the two trend charts, in one call — the
/// same "don't call a separate endpoint per KPI" shape <c>GetDashboardQuery</c> uses.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewReports)]
public record GetTeamPerformanceReportQuery : IRequest<Result<TeamPerformanceReportDto>>
{
    /// <summary>Inclusive lower bound on <c>Survey.Created</c>.</summary>
    public DateTimeOffset? FromDate { get; init; }

    /// <summary>Inclusive upper bound on <c>Survey.Created</c>.</summary>
    public DateTimeOffset? ToDate { get; init; }

    /// <summary>See <see cref="SurveyStatuses"/>. Filters which surveys count at all.</summary>
    public string? Status { get; init; }

    /// <summary>Restricts the report to one team; the KPIs then describe that team alone.</summary>
    public long? TeamId { get; init; }

    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
}

public sealed class GetTeamPerformanceReportQueryValidator : AbstractValidator<GetTeamPerformanceReportQuery>
{
    public GetTeamPerformanceReportQueryValidator()
    {
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value)
            .WithMessage("The end of the date range must not precede its start.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);

        RuleFor(x => x.Status)
            .Must(SurveyStatuses.IsDefined).WithMessage("Status is not a recognized survey status.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
    }
}

/// <summary>
/// A survey's responsible team is its <b>most recent</b> assignment row (by <c>AssignedDate</c>, tied
/// on <c>Id</c>) rather than the one flagged <c>IsActive</c> — an approved survey's assignment is no
/// longer active, and filtering on that flag would silently drop every completed survey from team
/// stats, which is exactly the work this report exists to measure.
/// </summary>
public sealed class GetTeamPerformanceReportQueryHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService)
    : IRequestHandler<GetTeamPerformanceReportQuery, Result<TeamPerformanceReportDto>>
{
    private const int DefaultTrendWeeks = 8;
    private const int DefaultWindowDays = 30;
    private const int TopTeamsForWeeklyChart = 4;

    public async Task<Result<TeamPerformanceReportDto>> Handle(
        GetTeamPerformanceReportQuery request, CancellationToken cancellationToken)
    {
        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);

        // Every query that lists surveys answers "may this caller see this row" the same way — see
        // OrgScopeQueryFilter's own doc comment. This report's grain is surveys aggregated by team,
        // so it is scoped like every other survey query rather than by each team's own territory
        // grant, which would answer a different question (can the caller see this *team* at all).
        var scopedSurveys = context.Surveys.AsNoTracking();
        if (!callerScope.IsUnrestricted)
        {
            scopedSurveys = scopedSurveys.Where(OrgScopeQueryFilter.ForSurveys(callerScope));
        }

        // GroupBy(a => a.SurveyId).Select(g => g.OrderByDescending(...).First()) does not translate
        // to SQL Server (EF throws at runtime, not compile time — verified live). The correlated
        // subquery form off the Survey.Assignments navigation, as GetSurveyTasksReportSummary already
        // uses, does translate.
        var query =
            from survey in scopedSurveys
            let teamId = survey.Assignments
                .OrderByDescending(a => a.AssignedDate)
                .ThenByDescending(a => a.Id)
                .Select(a => (long?)a.FieldTeamId)
                .FirstOrDefault()
            where teamId != null
            join team in context.FieldTeams.AsNoTracking() on teamId!.Value equals team.Id
            select new { survey, team };

        if (request.FromDate.HasValue)
        {
            query = query.Where(x => x.survey.Created >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(x => x.survey.Created <= request.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(x => x.survey.Status == request.Status);
        }

        if (request.TeamId.HasValue)
        {
            query = query.Where(x => x.team.Id == request.TeamId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.BranchCode))
        {
            query = query.Where(x => x.survey.BranchCode == request.BranchCode);
        }

        if (!string.IsNullOrWhiteSpace(request.OperationAreaCode))
        {
            query = query.Where(x => x.survey.OperationAreaCode == request.OperationAreaCode);
        }

        // A thin projection, bounded by the requested date range/status/scope rather than the whole
        // SURVEYS table, is pulled once and every calculation below (per-team stats, the aggregate
        // KPIs, both trend charts) runs against this one in-memory list. Several of the figures this
        // report needs — mean completion hours, distinct active days, ISO week buckets — do not
        // translate through EF Core's SQL Server LINQ provider, so there is no clean server-side
        // equivalent; the filtered survey set a report like this scopes to is the same kind of small,
        // bounded result the spec calls out as fine to bucket in memory for the charts, just pulled
        // once for the whole handler rather than a second time per chart.
        var rows = await query
            .Select(x => new SurveyPerformanceRow(
                x.team.Id,
                x.team.Name,
                x.survey.BranchCode,
                x.survey.Status,
                x.survey.AssignedDate,
                x.survey.CompletedDate,
                x.survey.DueDate))
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var teams = BuildTeamRows(rows, request, now);
        var kpis = BuildKpis(teams, rows);
        var (rangeStart, rangeEnd) = ResolveTrendRange(request);
        var weeklyCompletionTrend = BuildWeeklyCompletionTrend(rows, teams, rangeStart, rangeEnd);
        var productivityTrend = BuildProductivityTrend(rows, rangeStart, rangeEnd);

        return Result<TeamPerformanceReportDto>.Success(new TeamPerformanceReportDto
        {
            Kpis = kpis,
            Teams = teams,
            WeeklyCompletionTrend = weeklyCompletionTrend,
            ProductivityTrend = productivityTrend
        });
    }

    private static IReadOnlyList<TeamPerformanceRowDto> BuildTeamRows(
        IReadOnlyList<SurveyPerformanceRow> rows,
        GetTeamPerformanceReportQuery request,
        DateTimeOffset now)
    {
        var windowDays = ResolveWindowDays(request, now);

        return rows
            .GroupBy(x => (x.TeamId, x.TeamName))
            .Select(g =>
            {
                var assigned = g.Count();
                var completed = g.Count(x => x.Status == SurveyStatuses.Approved);
                var pending = g.Count(x => x.Status is SurveyStatuses.Created or SurveyStatuses.Assigned);
                var inProgress = g.Count(x => x.Status == SurveyStatuses.InProgress);
                var returned = g.Count(x => x.Status == SurveyStatuses.Returned);
                var overdue = g.Count(x => x.DueDate is not null
                    && x.DueDate < now
                    && x.Status is not (SurveyStatuses.Approved or SurveyStatuses.Expired));

                var completionHours = g
                    .Where(x => x.Status == SurveyStatuses.Approved && x.AssignedDate is not null && x.CompletedDate is not null)
                    .Select(x => (x.CompletedDate!.Value - x.AssignedDate!.Value).TotalHours)
                    .ToList();

                var avgCompletionHours = completionHours.Count > 0
                    ? Math.Round((decimal)completionHours.Average(), 2)
                    : 0m;

                var lateCompleted = g.Count(x => x.Status == SurveyStatuses.Approved
                    && x.CompletedDate is not null
                    && x.DueDate is not null
                    && x.CompletedDate > x.DueDate);

                var onTimeRate = completed > 0
                    ? Math.Round((completed - lateCompleted) * 100m / completed, 2)
                    : 0m;

                // Representative only — FieldTeam carries no branch of its own, so the first survey
                // this team worked in the window stands in for it.
                var branchCode = g.Select(x => x.BranchCode).FirstOrDefault(x => x is not null);

                return new TeamPerformanceRowDto
                {
                    TeamId = g.Key.TeamId,
                    TeamName = g.Key.TeamName,
                    BranchCode = branchCode,
                    Assigned = assigned,
                    Completed = completed,
                    Pending = pending,
                    InProgress = inProgress,
                    Returned = returned,
                    CompletionPercent = assigned > 0 ? Math.Round(completed * 100m / assigned, 2) : 0m,
                    AvgCompletionHours = avgCompletionHours,
                    DailyProductivity = windowDays > 0 ? Math.Round(completed / (decimal)windowDays, 2) : 0m,
                    OverdueCount = overdue,
                    OnTimeRatePercent = onTimeRate
                };
            })
            .OrderByDescending(x => x.Completed)
            .ToList();
    }

    private static int ResolveWindowDays(GetTeamPerformanceReportQuery request, DateTimeOffset now)
    {
        if (request.FromDate is null && request.ToDate is null)
        {
            return DefaultWindowDays;
        }

        var from = request.FromDate ?? request.ToDate!.Value.AddDays(-DefaultWindowDays);
        var to = request.ToDate ?? now;

        return Math.Max(1, (to.UtcDateTime.Date - from.UtcDateTime.Date).Days);
    }

    /// <summary>The Kpis are the sum across every team row — never a second, separately-computed query.</summary>
    private static TeamPerformanceKpisDto BuildKpis(
        IReadOnlyList<TeamPerformanceRowDto> teams,
        IReadOnlyList<SurveyPerformanceRow> rows)
    {
        var assigned = teams.Sum(x => x.Assigned);
        var completed = teams.Sum(x => x.Completed);
        var pending = teams.Sum(x => x.Pending) + teams.Sum(x => x.InProgress);
        var overdue = teams.Sum(x => x.OverdueCount);

        var completionRate = assigned > 0 ? Math.Round(completed * 100m / assigned, 2) : 0m;

        // Recomputed straight off the raw rows rather than averaged from the per-team figures, so
        // the aggregate mean is not skewed by teams with a tiny denominator.
        var completionHours = rows
            .Where(x => x.Status == SurveyStatuses.Approved && x.AssignedDate is not null && x.CompletedDate is not null)
            .Select(x => (x.CompletedDate!.Value - x.AssignedDate!.Value).TotalHours)
            .ToList();

        var avgCompletionHours = completionHours.Count > 0
            ? Math.Round((decimal)completionHours.Average(), 2)
            : 0m;

        return new TeamPerformanceKpisDto
        {
            AssignedTasks = assigned,
            CompletedTasks = completed,
            PendingTasks = pending,
            OverdueTasks = overdue,
            CompletionRatePercent = completionRate,
            AvgCompletionHours = avgCompletionHours
        };
    }

    private static (DateOnly Start, DateOnly End) ResolveTrendRange(GetTeamPerformanceReportQuery request)
    {
        var endDate = request.ToDate.HasValue
            ? DateOnly.FromDateTime(request.ToDate.Value.UtcDateTime)
            : DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        var startDate = request.FromDate.HasValue
            ? DateOnly.FromDateTime(request.FromDate.Value.UtcDateTime)
            : endDate.AddDays(-(DefaultTrendWeeks * 7));

        return (WeekStartOf(startDate), WeekStartOf(endDate));
    }

    /// <summary>Monday-anchored week start for <paramref name="date"/>.</summary>
    private static DateOnly WeekStartOf(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    private static List<DateOnly> BuildWeekBuckets(DateOnly start, DateOnly end)
    {
        var buckets = new List<DateOnly>();

        for (var week = start; week <= end; week = week.AddDays(7))
        {
            buckets.Add(week);
        }

        return buckets;
    }

    /// <summary>
    /// The top 4 teams by completed count, one point per (team, week) across the requested (or
    /// default 8-week) range — mirrors the frontend mock's 8-week default.
    /// </summary>
    private static IReadOnlyList<TeamWeeklyCompletionPointDto> BuildWeeklyCompletionTrend(
        IReadOnlyList<SurveyPerformanceRow> rows,
        IReadOnlyList<TeamPerformanceRowDto> teams,
        DateOnly rangeStart,
        DateOnly rangeEnd)
    {
        var topTeams = teams
            .OrderByDescending(x => x.Completed)
            .Take(TopTeamsForWeeklyChart)
            .Select(x => (x.TeamId, x.TeamName))
            .ToList();

        if (topTeams.Count == 0)
        {
            return [];
        }

        var buckets = BuildWeekBuckets(rangeStart, rangeEnd);
        var topTeamIds = topTeams.Select(x => x.TeamId).ToHashSet();

        var completions = rows
            .Where(x => topTeamIds.Contains(x.TeamId) && x.Status == SurveyStatuses.Approved && x.CompletedDate is not null)
            .Select(x => (x.TeamId, WeekStart: WeekStartOf(DateOnly.FromDateTime(x.CompletedDate!.Value.UtcDateTime))))
            .Where(x => x.WeekStart >= rangeStart && x.WeekStart <= rangeEnd)
            .ToList();

        var result = new List<TeamWeeklyCompletionPointDto>();

        foreach (var (teamId, teamName) in topTeams)
        {
            foreach (var week in buckets)
            {
                var count = completions.Count(x => x.TeamId == teamId && x.WeekStart == week);

                result.Add(new TeamWeeklyCompletionPointDto
                {
                    TeamId = teamId,
                    TeamName = teamName,
                    WeekStart = week,
                    CompletedCount = count
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Same weekly buckets across every visible team: total completions that week divided by how
    /// many distinct teams contributed one, not the whole roster — a week where only two teams
    /// finished anything should not be diluted by teams that had nothing due.
    /// </summary>
    private static IReadOnlyList<ProductivityTrendPointDto> BuildProductivityTrend(
        IReadOnlyList<SurveyPerformanceRow> rows,
        DateOnly rangeStart,
        DateOnly rangeEnd)
    {
        var buckets = BuildWeekBuckets(rangeStart, rangeEnd);

        var completions = rows
            .Where(x => x.Status == SurveyStatuses.Approved && x.CompletedDate is not null)
            .Select(x => (x.TeamId, WeekStart: WeekStartOf(DateOnly.FromDateTime(x.CompletedDate!.Value.UtcDateTime))))
            .Where(x => x.WeekStart >= rangeStart && x.WeekStart <= rangeEnd)
            .ToList();

        var result = new List<ProductivityTrendPointDto>();

        foreach (var week in buckets)
        {
            var weekCompletions = completions.Where(x => x.WeekStart == week).ToList();
            var totalCompleted = weekCompletions.Count;
            var distinctTeamCount = weekCompletions.Select(x => x.TeamId).Distinct().Count();

            result.Add(new ProductivityTrendPointDto
            {
                WeekStart = week,
                AvgCompletedPerTeam = distinctTeamCount > 0
                    ? Math.Round(totalCompleted / (decimal)distinctTeamCount, 2)
                    : 0m
            });
        }

        return result;
    }

    /// <summary>
    /// Slice-local projection — not part of the public report contract, just the thin row every
    /// calculation above is built from.
    /// </summary>
    private sealed record SurveyPerformanceRow(
        long TeamId,
        string TeamName,
        string? BranchCode,
        string Status,
        DateTimeOffset? AssignedDate,
        DateTimeOffset? CompletedDate,
        DateTimeOffset? DueDate);
}
