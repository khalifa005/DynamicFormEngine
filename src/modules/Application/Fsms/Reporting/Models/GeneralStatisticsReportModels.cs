namespace KH.Application.Fsms.Reporting.Models;

/// <summary>
/// The Reports > General Statistics page: KPIs against the preceding period, the weekly submission
/// trend, the full status split, per-team completion, recent activity, and the two late-work tables.
/// Reuses <see cref="DashboardDto"/>'s underlying data (via <c>IDashboardStore</c>) rather than
/// re-querying — the dashboard and this report read the same filtered survey set.
/// </summary>
public sealed record GeneralStatisticsReportDto
{
    public required GeneralStatisticsKpisDto Kpis { get; init; }
    public required IReadOnlyList<DashboardTrendPointDto> Trend { get; init; }
    public required IReadOnlyList<DashboardStatusSliceDto> StatusDistribution { get; init; }
    public required IReadOnlyList<GeneralStatisticsTeamCompletionDto> TeamCompletion { get; init; }
    public required IReadOnlyList<DashboardRecentSurveyDto> RecentActivity { get; init; }
    public required IReadOnlyList<DashboardLateSurveyDto> LateTasks { get; init; }
    public required IReadOnlyList<DashboardLateTeamDto> LateTeams { get; init; }
}

public sealed record GeneralStatisticsKpisDto
{
    public int TotalTasks { get; init; }
    public int OverdueTasks { get; init; }
    public int TotalTeams { get; init; }
    public int ActiveTeams { get; init; }
    public decimal AvgCompletionHours { get; init; }
    public decimal CompletionRatePercent { get; init; }
    public decimal ReturnRatePercent { get; init; }
    public decimal OnTimeRatePercent { get; init; }
    public decimal TotalTasksDelta { get; init; }
    public decimal OverdueTasksDelta { get; init; }
    public decimal ActiveTeamsDelta { get; init; }
    public decimal CompletionRateDelta { get; init; }
    public decimal ReturnRateDelta { get; init; }
    public decimal OnTimeRateDelta { get; init; }
}

public sealed record GeneralStatisticsTeamCompletionDto
{
    public long TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public int Assigned { get; init; }
    public int Completed { get; init; }
    public decimal CompletionPercent { get; init; }
}
