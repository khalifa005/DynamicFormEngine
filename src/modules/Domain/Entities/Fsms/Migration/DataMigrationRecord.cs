using KH.Domain.Constants.Fsms;

namespace KH.Domain.Entities.Fsms.Migration;

/// <summary>
/// What became of one row of an imported file. Written for every record, not only the failures:
/// an import of several hundred records is only trustworthy if the operator can point at any one of
/// them and see which survey it became — and the ones that were skipped are exactly the evidence
/// that a re-run did not duplicate anything. Table: <c>DATA_MIGRATION_RECORDS</c>.
/// </summary>
public sealed class DataMigrationRecord : BaseAuditableEntity<long>
{
    /// <summary>Long enough for a stack-free failure reason; anything longer is noise in a list.</summary>
    private const int MaxMessageLength = 1000;

    private DataMigrationRecord()
    {
    }

    private DataMigrationRecord(
        long runId,
        int rowNumber,
        string? externalId,
        string status,
        long? surveyId,
        string? surveyCode,
        long? submissionId,
        int filesImported,
        int filesMissing,
        string? message)
    {
        RunId = runId;
        RowNumber = rowNumber;
        ExternalId = externalId;
        Status = status;
        SurveyId = surveyId;
        SurveyCode = surveyCode;
        SubmissionId = submissionId;
        FilesImported = filesImported;
        FilesMissing = filesMissing;
        Message = message;
        IsActive = true;
    }

    public long RunId { get; private set; }

    /// <summary>1-based row in the source file, so a failure can be found in the operator's own copy.</summary>
    public int RowNumber { get; private set; }

    /// <summary>The source system's own key for the record (Fulcrum: <c>fulcrum_id</c>).</summary>
    public string? ExternalId { get; private set; }

    /// <summary>See <see cref="MigrationRecordStatuses"/>.</summary>
    public string Status { get; private set; } = default!;

    public long? SurveyId { get; private set; }
    public string? SurveyCode { get; private set; }
    public long? SubmissionId { get; private set; }

    public int FilesImported { get; private set; }
    public int FilesMissing { get; private set; }

    /// <summary>Why it failed, or which media were missing. Null when there is nothing to say.</summary>
    public string? Message { get; private set; }

    public static DataMigrationRecord Imported(
        int rowNumber,
        string? externalId,
        long surveyId,
        string surveyCode,
        long submissionId,
        int filesImported,
        int filesMissing,
        string? message) =>
        new(
            runId: 0,
            rowNumber,
            Normalize(externalId),
            MigrationRecordStatuses.Imported,
            surveyId,
            surveyCode,
            submissionId,
            filesImported,
            filesMissing,
            Truncate(message));

    /// <summary>
    /// Nothing to do for this row. The message is optional because the ordinary reason — already
    /// imported — is obvious from the run, but a backfill pass skips rows for a reason that is not
    /// ("never imported in the first place"), and an unexplained skip there would look like a bug.
    /// </summary>
    public static DataMigrationRecord Skipped(
        int rowNumber,
        string? externalId,
        long? surveyId,
        string? surveyCode,
        string? message = null) =>
        new(
            runId: 0,
            rowNumber,
            Normalize(externalId),
            MigrationRecordStatuses.Skipped,
            surveyId,
            surveyCode,
            submissionId: null,
            filesImported: 0,
            filesMissing: 0,
            Truncate(message));

    public static DataMigrationRecord Failed(int rowNumber, string? externalId, string message) =>
        new(
            runId: 0,
            rowNumber,
            Normalize(externalId),
            MigrationRecordStatuses.Failed,
            surveyId: null,
            surveyCode: null,
            submissionId: null,
            filesImported: 0,
            filesMissing: 0,
            Truncate(message));

    /// <summary>A validate-only outcome: the row was read and checked, and nothing was written.</summary>
    public static DataMigrationRecord Validated(
        int rowNumber,
        string? externalId,
        int filesFound,
        int filesMissing,
        string? message) =>
        new(
            runId: 0,
            rowNumber,
            Normalize(externalId),
            MigrationRecordStatuses.Validated,
            surveyId: null,
            surveyCode: null,
            submissionId: null,
            filesFound,
            filesMissing,
            Truncate(message));

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Truncate(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var trimmed = message.Trim();
        return trimmed.Length <= MaxMessageLength ? trimmed : trimmed[..MaxMessageLength];
    }
}
