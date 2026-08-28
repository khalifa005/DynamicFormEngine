namespace KH.Application.Fsms.Reporting.Models;

/// <summary>
/// Everything the dashboard renders. Assembled from the ten result sets of
/// <c>usp_Fsms_Dashboard_Get</c> in one round trip — the widgets all read the same filtered
/// survey set, so splitting them across queries would mean paying for that filter ten times.
/// </summary>
public sealed record DashboardDto
{
    public required DashboardKpisDto Kpis { get; init; }
    public required IReadOnlyList<DashboardTrendPointDto> Trend { get; init; }
    public required IReadOnlyList<DashboardStatusSliceDto> StatusDistribution { get; init; }
    public required IReadOnlyList<DashboardOrgTeamStatsDto> TeamsByOrg { get; init; }
    public required IReadOnlyList<DashboardDepartmentTeamStatsDto> TeamsByDepartment { get; init; }
    public required DashboardMyStatsDto MyStats { get; init; }
    public required IReadOnlyList<DashboardPeerStatDto> PeerComparison { get; init; }
    public required IReadOnlyList<DashboardTransactionDto> Transactions { get; init; }
    public required IReadOnlyList<DashboardRecentSurveyDto> RecentSurveys { get; init; }
    public required IReadOnlyList<DashboardBreakdownDto> Breakdowns { get; init; }
    public required IReadOnlyList<DashboardLateSurveyDto> LateSurveys { get; init; }
    public required IReadOnlyList<DashboardLateTeamDto> LateTeams { get; init; }
}

/// <summary>
/// Headline counters, each paired with its percentage change against the window of equal length
/// immediately before the one requested.
/// </summary>
public sealed record DashboardKpisDto
{
    public DateTimeOffset FromDate { get; init; }
    public DateTimeOffset ToDate { get; init; }

    public int TotalSurveys { get; init; }
    public int ApprovedSurveys { get; init; }
    public int ReturnedSurveys { get; init; }
    public int InProgressSurveys { get; init; }

    /// <summary>
    /// Filled work awaiting a reviewer's decision. <c>SUBMITTED</c> is that state outright — a filled
    /// survey is completed or returned straight from it.
    /// </summary>
    public int SubmittedSurveys { get; init; }

    /// <summary>
    /// Still-open fill past <c>DueDate</c>, or awaiting-review past <c>CompletionDueDate</c>.
    /// Approved work that ran late is not overdue.
    /// </summary>
    public int OverdueSurveys { get; init; }

    /// <summary>Distinct teams currently holding at least one survey in the window.</summary>
    public int ActiveTeams { get; init; }

    /// <summary>Mean hours from allocation to completion — the clock a field team is judged on.</summary>
    public decimal AvgCompletionHours { get; init; }

    public decimal CompletionRatePercent { get; init; }
    public decimal ReturnRatePercent { get; init; }
    public decimal OnTimeRatePercent { get; init; }

    public decimal TotalSurveysDelta { get; init; }
    public decimal ApprovedSurveysDelta { get; init; }
    public decimal SubmittedSurveysDelta { get; init; }
    public decimal ReturnedSurveysDelta { get; init; }
    public decimal InProgressSurveysDelta { get; init; }
    public decimal OverdueSurveysDelta { get; init; }
    public decimal ActiveTeamsDelta { get; init; }
    public decimal CompletionRateDelta { get; init; }
    public decimal ReturnRateDelta { get; init; }
    public decimal OnTimeRateDelta { get; init; }
}

/// <summary>
/// One bucket of the trend chart. Counts transitions rather than survey date columns, so a survey
/// returned twice shows two returns. Empty buckets are present with zeroes to keep the x-axis even.
/// </summary>
public sealed record DashboardTrendPointDto
{
    public DateTime Bucket { get; init; }

    /// <summary>ISO date of the bucket start; the client formats it for the current language.</summary>
    public string BucketKey { get; init; } = string.Empty;

    public int CreatedCount { get; init; }
    public int AssignedCount { get; init; }
    public int SubmittedCount { get; init; }
    public int ApprovedCount { get; init; }
    public int ReturnedCount { get; init; }
}

/// <summary>One slice of the status doughnut. Every status is returned, including the zeroes.</summary>
public sealed record DashboardStatusSliceDto
{
    public string Status { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal Percent { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>
/// Team coverage and workload for one node of the org geography. All three levels come back in one
/// list, tagged by <see cref="Level"/>, so the client can pivot without another call.
/// </summary>
public sealed record DashboardOrgTeamStatsDto
{
    /// <summary>One of <c>Cbu</c>, <c>Branch</c>, <c>OperationArea</c>.</summary>
    public string Level { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;
    public string? NameEn { get; init; }
    public string? NameAr { get; init; }
    public string? ParentCode { get; init; }

    /// <summary>Teams whose org scope reaches this node, counting grants made further up.</summary>
    public int TeamCount { get; init; }

    /// <summary>Teams actually holding a survey here in the window — coverage minus the idle.</summary>
    public int WorkingTeamCount { get; init; }

    public int MemberCount { get; init; }
    public int SurveyCount { get; init; }
    public int OpenSurveyCount { get; init; }
    public int ApprovedSurveyCount { get; init; }
    public decimal SurveysPerTeam { get; init; }
    public decimal AvgCompletionHours { get; init; }
}

public sealed record DashboardDepartmentTeamStatsDto
{
    public int DepartmentId { get; init; }
    public string? NameEn { get; init; }
    public string? NameAr { get; init; }
    public int TeamCount { get; init; }
    public int SurveyCount { get; init; }
    public int OpenSurveyCount { get; init; }
    public int ApprovedSurveyCount { get; init; }
    public decimal SurveysPerTeam { get; init; }
}

/// <summary>
/// The signed-in user's own throughput over the window. Always present, zeroed when they did
/// nothing, so the client never has to handle an absent record.
/// </summary>
public sealed record DashboardMyStatsDto
{
    public string? UserId { get; init; }
    public string? DisplayName { get; init; }
    public string? RoleName { get; init; }

    /// <summary>Status transitions the user performed — their transaction count.</summary>
    public int TransactionCount { get; init; }

    public int SurveysTouched { get; init; }
    public int AssignedCount { get; init; }
    public int SubmittedCount { get; init; }
    public int ApprovedCount { get; init; }
    public int ReturnedCount { get; init; }

    /// <summary>Mean hours between a survey reaching the user and their acting on it.</summary>
    public decimal AvgHandlingHours { get; init; }

    public DateTimeOffset? LastActivityDate { get; init; }
    public int CohortSize { get; init; }
    public string? PeerScope { get; init; }
}

/// <summary>
/// One peer in the caller's cohort. Ranked across the whole cohort before trimming, and the
/// caller's own row is always present even when they fall outside the top N.
/// </summary>
public sealed record DashboardPeerStatDto
{
    public string UserId { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string? RoleName { get; init; }
    public bool IsCurrentUser { get; init; }
    public int TransactionCount { get; init; }
    public int SurveysTouched { get; init; }
    public int ApprovedCount { get; init; }
    public int ReturnedCount { get; init; }
    public decimal AvgHandlingHours { get; init; }
    public DateTimeOffset? LastActivityDate { get; init; }
    public int Rank { get; init; }
    public decimal PercentileRank { get; init; }
    public decimal CohortAvg { get; init; }
    public decimal CohortMedian { get; init; }
    public int CohortMax { get; init; }
    public int CohortSize { get; init; }
}

/// <summary>
/// One recorded action by one person. Drawn from the append-only status trail, which is the only
/// place a person's work is recorded, and never widened beyond the surveys the caller may see.
/// </summary>
public sealed record DashboardTransactionDto
{
    public long HistoryId { get; init; }
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public string? DisplayName { get; init; }
    public string? RoleName { get; init; }
    public string? FromStatus { get; init; }
    public string ToStatus { get; init; } = string.Empty;
    public DateTimeOffset ChangedDate { get; init; }
    public string? Note { get; init; }
    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public string? FieldTeamName { get; init; }
    public bool IsCurrentUser { get; init; }
}

public sealed record DashboardRecentSurveyDto
{
    public long Id { get; init; }
    public string SurveyCode { get; init; } = string.Empty;
    public long TemplateId { get; init; }
    public string? TemplateNameEn { get; init; }
    public string? TemplateNameAr { get; init; }
    public string? BranchCode { get; init; }
    public string? BranchNameEn { get; init; }
    public string? BranchNameAr { get; init; }
    public string? CbuCode { get; init; }
    public long? FieldTeamId { get; init; }
    public string? FieldTeamName { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public bool IsOverdue { get; init; }
}

/// <summary>How a survey came to be late — see <see cref="DashboardLateSurveyDto.LatenessKind"/>.</summary>
public abstract class DashboardLatenessKinds
{
    /// <summary>Still open past fill due, or SUBMITTED past completion due. Actionable now.</summary>
    public const string Overdue = "OVERDUE";

    /// <summary>Finished, but missed fill and/or completion deadline. History rather than a to-do.</summary>
    public const string CompletedLate = "COMPLETED_LATE";
}

/// <summary>Which SLA clock was breached — see <see cref="DashboardLateSurveyDto.DeadlineKind"/>.</summary>
public abstract class DashboardDeadlineKinds
{
    public const string Fill = "FILL";
    public const string Completion = "COMPLETION";
}

/// <summary>
/// One survey that missed a deadline. Covers overdue vs completed-late via
/// <see cref="LatenessKind"/>, and fill vs completion via <see cref="DeadlineKind"/>.
/// </summary>
public sealed record DashboardLateSurveyDto
{
    public long Id { get; init; }
    public string SurveyCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    /// <summary>See <see cref="DashboardLatenessKinds"/>.</summary>
    public string LatenessKind { get; init; } = string.Empty;

    /// <summary>See <see cref="DashboardDeadlineKinds"/>. The breached clock; <see cref="DueDate"/> is that deadline.</summary>
    public string DeadlineKind { get; init; } = string.Empty;

    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? CompletedDate { get; init; }

    /// <summary>Open work is measured against now; finished work against when it landed.</summary>
    public int DaysLate { get; init; }

    public long? FieldTeamId { get; init; }
    public string? FieldTeamName { get; init; }
    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? BranchNameEn { get; init; }
    public string? BranchNameAr { get; init; }
    public string? TemplateNameEn { get; init; }
    public string? TemplateNameAr { get; init; }
    public DateTimeOffset Created { get; init; }
}

/// <summary>
/// Lateness rolled up to the team that held the work, so "who is behind" has an answer that is not
/// a survey list. Only teams carrying at least one late survey are returned.
/// </summary>
public sealed record DashboardLateTeamDto
{
    public long FieldTeamId { get; init; }
    public string? FieldTeamName { get; init; }
    public string? UserCode { get; init; }

    /// <summary>Every survey the team held in the window, late or not — the denominator.</summary>
    public int SurveyCount { get; init; }

    /// <summary>Still open and past due.</summary>
    public int OverdueCount { get; init; }

    /// <summary>Finished after the due date.</summary>
    public int CompletedLateCount { get; init; }

    public int OnTimeCount { get; init; }

    /// <summary>Averaged over the late surveys only, so clean work does not dilute it toward zero.</summary>
    public decimal AvgDaysLate { get; init; }

    /// <summary>The worst still-open survey. Zero when nothing is currently overdue.</summary>
    public int MaxDaysOverdue { get; init; }

    public decimal OnTimeRatePercent { get; init; }
}

/// <summary>
/// A count by one dimension. Four dimensions share one shape — <c>FaType</c>, <c>ReturnReason</c>,
/// <c>Template</c>, <c>Source</c> — because the client renders them all with the same table.
/// </summary>
public sealed record DashboardBreakdownDto
{
    public string Dimension { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? NameEn { get; init; }
    public string? NameAr { get; init; }
    public int Count { get; init; }
    public decimal Percent { get; init; }
}
