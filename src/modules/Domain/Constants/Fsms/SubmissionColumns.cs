namespace KH.Domain.Constants.Fsms;

/// <summary>
/// The fixed columns every row of the shared <c>SUBMISSIONS</c> table carries, whatever template
/// wrote it. Everything else on a row is a template field keyed by its <c>data_name</c>, so this
/// list is what separates a submission's metadata from its answers.
/// </summary>
public abstract class SubmissionColumns
{
    public const string Id = "Id";
    public const string TemplateId = "TemplateId";
    public const string VersionNo = "VersionNo";
    public const string Status = "Status";
    public const string SurveyId = "SurveyId";
    public const string AssignmentId = "AssignmentId";
    public const string FilledByRole = "FilledByRole";
    public const string SubmittedBy = "SubmittedBy";
    public const string SubmittedDate = "SubmittedDate";
    public const string Created = "Created";

    /// <summary>
    /// The offline client's own key for the fill. Uniquely indexed per template, so replaying a
    /// queued submission after a dropped connection cannot write it a second time.
    /// </summary>
    public const string ClientSubmissionId = "ClientSubmissionId";

    public static readonly IReadOnlyList<string> All =
    [
        Id,
        TemplateId,
        VersionNo,
        Status,
        SurveyId,
        AssignmentId,
        FilledByRole,
        SubmittedBy,
        SubmittedDate,
        Created,
        ClientSubmissionId
    ];

    /// <summary>Whether a column name is metadata rather than an answer.</summary>
    public static bool IsBase(string? column) =>
        column is not null && All.Contains(column, StringComparer.OrdinalIgnoreCase);
}
