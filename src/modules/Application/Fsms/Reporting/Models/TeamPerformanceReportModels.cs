namespace KH.Application.Fsms.Reporting.Models;

/// <summary>
/// Everything the Team Performance report needs in one round trip — summary KPIs, a per-team
/// breakdown and the two trend charts — following the same "one call, not one per KPI" shape as
/// <c>DashboardDto</c>.
/// </summary>
public sealed record TeamPerformanceReportDto
{
    public required TeamPerformanceKpisDto Kpis { get; init; }
    public required IReadOnlyList<TeamPerformanceRowDto> Teams { get; init; }
    public required IReadOnlyList<TeamWeeklyCompletionPointDto> WeeklyCompletionTrend { get; init; }
    public required IReadOnlyList<ProductivityTrendPointDto> ProductivityTrend { get; init; }
}

/// <summary>The headline numbers — the sum across every team row, not a separately-queried total.</summary>
public sealed record TeamPerformanceKpisDto
{
    public int AssignedTasks { get; init; }
    public int CompletedTasks { get; init; }

    /// <summary>Created + Assigned + InProgress.</summary>
    public int PendingTasks { get; init; }

    public int OverdueTasks { get; init; }
    public decimal CompletionRatePercent { get; init; }

    /// <summary>Mean of (CompletedDate - AssignedDate) over completed surveys with both dates set.</summary>
    public decimal AvgCompletionHours { get; init; }
}

public sealed record TeamPerformanceRowDto
{
    public long TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public string? BranchCode { get; init; }
    public int Assigned { get; init; }
    public int Completed { get; init; }
    public int Pending { get; init; }
    public int InProgress { get; init; }
    public int Returned { get; init; }
    public decimal CompletionPercent { get; init; }
    public decimal AvgCompletionHours { get; init; }

    /// <summary>Completed divided by the number of days in the requested (or default) window.</summary>
    public decimal DailyProductivity { get; init; }

    public int OverdueCount { get; init; }

    /// <summary>(Completed - lateCompletedCount) * 100 / Completed, 0 when Completed = 0.</summary>
    public decimal OnTimeRatePercent { get; init; }
}

public sealed record TeamWeeklyCompletionPointDto
{
    public long TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public DateOnly WeekStart { get; init; }
    public int CompletedCount { get; init; }
}

public sealed record ProductivityTrendPointDto
{
    public DateOnly WeekStart { get; init; }
    public decimal AvgCompletedPerTeam { get; init; }
}
