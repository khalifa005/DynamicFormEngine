namespace KH.Application.Fsms.Surveys.Models;

public sealed class SurveyListItemDto
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;
    public long TemplateId { get; init; }
    public string? TemplateCode { get; init; }
    public string? TemplateNameEn { get; init; }
    public string? TemplateNameAr { get; init; }
    public int? TemplateVersionNo { get; init; }

    /// <summary>
    /// The template's newest published version. Ahead of <see cref="TemplateVersionNo"/> means the
    /// survey is pinned to an older form and can be migrated onto the newer one.
    /// </summary>
    public int? TemplateCurrentVersionNo { get; init; }

    public string Source { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string? FaId { get; init; }
    public string? TaskCode { get; init; }
    public string? FaTypeCode { get; init; }
    public long? TaskTypeId { get; init; }
    public string? TaskTypeNameEn { get; init; }
    public string? TaskTypeNameAr { get; init; }
    public string? CustomerName { get; init; }

    /// <summary>
    /// The number the crew calls before arriving. Carried on the list row so the worklist can offer
    /// it without a second request, even though the table does not show a column for it today.
    /// </summary>
    public string? CustomerPhone { get; init; }

    public long? CustomerTypeId { get; init; }
    public string? CustomerTypeNameEn { get; init; }
    public string? CustomerTypeNameAr { get; init; }
    public string? MeterNumber { get; init; }
    public string? Hcn { get; init; }
    public bool IsExternalTask { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? CbuCode { get; init; }
    public string? CbuNameEn { get; init; }
    public string? CbuNameAr { get; init; }
    public string? BranchCode { get; init; }
    public string? BranchNameEn { get; init; }
    public string? BranchNameAr { get; init; }
    public string? OperationAreaCode { get; init; }
    public string? OperationAreaNameEn { get; init; }
    public string? OperationAreaNameAr { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentNameEn { get; init; }
    public string? DepartmentNameAr { get; init; }

    /// <summary>The team currently holding the survey; only one ever does.</summary>
    public long? AllocatedFieldTeamId { get; init; }

    public string? AllocatedFieldTeamName { get; init; }

    /// <summary>Fill deadline (team must submit by this time).</summary>
    public DateTimeOffset? DueDate { get; init; }

    /// <summary>Back-office completion (approve) deadline.</summary>
    public DateTimeOffset? CompletionDueDate { get; init; }

    /// <summary>Snapshot of the template team-fill SLA (calendar hours) at create.</summary>
    public int? TeamFillSlaHours { get; init; }

    /// <summary>Snapshot of the template completion SLA (calendar hours) at create.</summary>
    public int? CompletionSlaHours { get; init; }

    public DateTimeOffset? AssignedDate { get; init; }
    public DateTimeOffset? SubmittedDate { get; init; }
    public string? LastFilledByRole { get; init; }
    public int SubmissionCount { get; init; }

    /// <summary>
    /// Why the survey was sent back — a <c>LKP_RETURN_REASON</c> code. Present only while the survey
    /// is <c>RETURNED</c>, and what the worklist tags the row with so the crew notices it.
    /// </summary>
    public string? ReturnReasonCode { get; init; }

    public string? ReturnReason { get; init; }
    public DateTimeOffset? ReturnedDate { get; init; }

    /// <summary>How many times this survey has come back. Non-zero flags a repeat offender.</summary>
    public int ReturnCount { get; init; }

    public DateTimeOffset Created { get; init; }
    public DateTimeOffset LastModified { get; init; }
}
