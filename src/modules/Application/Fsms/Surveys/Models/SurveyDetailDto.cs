namespace KH.Application.Fsms.Surveys.Models;

public sealed class SurveyDetailDto
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;
    public long TemplateId { get; init; }
    public string? TemplateCode { get; init; }
    public string? TemplateNameEn { get; init; }
    public string? TemplateNameAr { get; init; }

    /// <summary>The published version the survey is pinned to; a later republish never moves it.</summary>
    public long? TemplateVersionId { get; init; }

    public int? TemplateVersionNo { get; init; }

    /// <summary>
    /// The form-builder definition frozen at the pinned version. Falls back to the template's
    /// current definition when the survey predates any publish.
    /// </summary>
    public string DefinitionJson { get; init; } = "{}";

    public string Source { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string? FaId { get; init; }
    public string? TaskCode { get; init; }
    public string? FaTypeCode { get; init; }
    public long? TaskTypeId { get; init; }
    public string? TaskTypeNameEn { get; init; }
    public string? TaskTypeNameAr { get; init; }
    public string? CustomerName { get; init; }

    /// <summary>The number the crew calls before arriving.</summary>
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
    /// <summary>Fill deadline (team must submit by this time).</summary>
    public DateTimeOffset? DueDate { get; init; }

    /// <summary>Back-office completion (approve) deadline.</summary>
    public DateTimeOffset? CompletionDueDate { get; init; }

    /// <summary>Snapshot of the template team-fill SLA (calendar hours) at create.</summary>
    public int? TeamFillSlaHours { get; init; }

    /// <summary>Snapshot of the template completion SLA (calendar hours) at create.</summary>
    public int? CompletionSlaHours { get; init; }

    /// <summary>The originating system's dispatch instruction for the crew (C2M's <c>comment</c>).</summary>
    public string? SourceComment { get; init; }

    public string AdditionalDataJson { get; init; } = "{}";

    /// <summary>Roll-up of every fill, keyed by fill role.</summary>
    public string ResultSummaryJson { get; init; } = "{}";

    public string? AssignedBy { get; init; }
    public DateTimeOffset? AssignedDate { get; init; }
    public DateTimeOffset? StartedDate { get; init; }
    public DateTimeOffset? SubmittedDate { get; init; }
    public string? LastFilledByRole { get; init; }
    public int SubmissionCount { get; init; }
    public string? ReceivedBy { get; init; }
    public DateTimeOffset? ReceivedDate { get; init; }
    public string? CompletedBy { get; init; }
    public DateTimeOffset? CompletedDate { get; init; }
    public string? ReturnReason { get; init; }

    /// <summary>The <c>LKP_RETURN_REASON</c> code behind the current return; null once approved.</summary>
    public string? ReturnReasonCode { get; init; }

    public string? ReturnedBy { get; init; }
    public DateTimeOffset? ReturnedDate { get; init; }

    /// <summary>How many times the survey has been sent back; survives approval.</summary>
    public int ReturnCount { get; init; }

    public DateTimeOffset Created { get; init; }
    public string? CreatedBy { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public string? LastModifiedBy { get; init; }
    public bool IsActive { get; init; }

    /// <summary>
    /// Display names for every account id this document names — the lifecycle stamps above and the
    /// <c>filledBy</c> of each fill inside <see cref="ResultSummaryJson"/> — keyed by id. Carried as
    /// a map rather than a name beside each stamp because the fills live inside an opaque JSON
    /// string, and a reader that has to show "filled by" cannot be asked to resolve ids itself.
    /// An id with no account is absent, so the client falls back to showing the id.
    /// </summary>
    public IReadOnlyDictionary<string, string> UserNames { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<SurveyAssignmentDto> Assignments { get; init; } = [];
}
