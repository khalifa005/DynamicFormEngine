namespace KH.Application.Fsms.Reporting.Models;

/// <summary>
/// Everything the "Survey Tasks" report's summary screen renders in one round trip — KPIs, the
/// charts around them, and the worklist of tasks that missed a deadline. Split from the paginated
/// detail table (<see cref="SurveyTaskReportRowDto"/>) because the two are read at different times
/// by different widgets: the summary is read once per filter change, the detail table is read once
/// per page turn, and folding the (potentially large) row-by-row table into this response would mean
/// paying for it on every KPI refresh.
/// </summary>
public sealed record SurveyTasksReportSummaryDto
{
    public required SurveyTasksReportKpisDto Kpis { get; init; }
    public required IReadOnlyList<SurveyTasksDailyTrendPointDto> DailyTrend { get; init; }
    public required IReadOnlyList<SurveyTaskStatusSliceDto> StatusDistribution { get; init; }
    public required IReadOnlyList<SurveyTasksByTeamDto> TasksByTeam { get; init; }
    public required IReadOnlyList<SurveyTasksByBranchDto> TasksByBranch { get; init; }
    public required IReadOnlyList<SurveyTaskLateItemDto> LateTasks { get; init; }
}

/// <summary>Headline counters for the filtered survey-task set.</summary>
public sealed record SurveyTasksReportKpisDto
{
    public int TotalTasks { get; init; }

    /// <summary>Not yet allocated or not yet started — <c>CREATED</c> + <c>ASSIGNED</c>.</summary>
    public int PendingTasks { get; init; }

    public int InProgressTasks { get; init; }
    public int SubmittedTasks { get; init; }
    public int ApprovedTasks { get; init; }
    public int ReturnedTasks { get; init; }

    /// <summary>Still open (not <c>APPROVED</c>/<c>EXPIRED</c>) and past <see cref="Domain.Entities.Fsms.Surveys.Survey.DueDate"/>.</summary>
    public int OverdueTasks { get; init; }

    /// <summary><see cref="ApprovedTasks"/> as a percentage of <see cref="TotalTasks"/>; zero when there are none.</summary>
    public decimal CompletionRatePercent { get; init; }
}

/// <summary>One day of the "tasks created" trend line. Only days with a survey are returned.</summary>
public sealed record SurveyTasksDailyTrendPointDto
{
    public DateOnly Date { get; init; }
    public int CreatedCount { get; init; }
}

/// <summary>One slice of the status doughnut. Every <see cref="Domain.Constants.Fsms.SurveyStatuses"/> value is present, zero-filled.</summary>
public sealed record SurveyTaskStatusSliceDto
{
    public string Status { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal Percent { get; init; }
}

/// <summary>Task count for one field team, keyed by that team's most recent assignment.</summary>
public sealed record SurveyTasksByTeamDto
{
    public long? TeamId { get; init; }

    /// <summary><see cref="ReportUnassignedLabels.Team"/> when the task has never been allocated.</summary>
    public string TeamName { get; init; } = string.Empty;

    public int Count { get; init; }
}

/// <summary>Task count for one branch.</summary>
public sealed record SurveyTasksByBranchDto
{
    public string? BranchCode { get; init; }

    /// <summary><see cref="ReportUnassignedLabels.Branch"/> when the task carries no branch.</summary>
    public string BranchName { get; init; } = string.Empty;

    public int Count { get; init; }
}

/// <summary>
/// One survey that missed a deadline — either still open past due (<see cref="SurveyTaskLatenessKinds.Overdue"/>)
/// or approved after its due date (<see cref="SurveyTaskLatenessKinds.CompletedLate"/>).
/// </summary>
public sealed record SurveyTaskLateItemDto
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    /// <summary>See <see cref="SurveyTaskLatenessKinds"/>.</summary>
    public string LatenessKind { get; init; } = string.Empty;

    /// <summary>Open work is measured against now; finished work against when it was approved.</summary>
    public int DaysLate { get; init; }

    public DateTimeOffset? DueDate { get; init; }
    public long? TeamId { get; init; }
    public string? TeamName { get; init; }
    public string? BranchCode { get; init; }
    public string? TemplateNameEn { get; init; }
}

/// <summary>See <see cref="SurveyTaskLateItemDto.LatenessKind"/>.</summary>
public abstract class SurveyTaskLatenessKinds
{
    /// <summary>Still open, past <c>DueDate</c>. Actionable now.</summary>
    public const string Overdue = "OVERDUE";

    /// <summary>Approved, but past <c>DueDate</c> when it happened. History rather than a to-do.</summary>
    public const string CompletedLate = "COMPLETED_LATE";
}

/// <summary>Display labels used in place of a name the report could not resolve — no free-text magic strings.</summary>
public abstract class ReportUnassignedLabels
{
    public const string Team = "Unassigned";
    public const string Branch = "Unassigned";
}

/// <summary>One row of the paginated survey-task detail table.</summary>
public sealed record SurveyTaskReportRowDto
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = string.Empty;
    public string? TemplateNameEn { get; init; }
    public string? TemplateNameAr { get; init; }
    public int? TemplateVersionNo { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string? BranchCode { get; init; }
    public string? BranchNameEn { get; init; }
    public string? BranchNameAr { get; init; }
    public int? DepartmentId { get; init; }
    public long? TeamId { get; init; }
    public string? TeamName { get; init; }
    public string? CustomerName { get; init; }
    public string? FaId { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public int SubmissionCount { get; init; }
}
