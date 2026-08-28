using KH.Domain.Entities.Fsms.Templates;

namespace KH.Application.Fsms.Submissions.Interfaces;

/// <summary>
/// Native-SQL data access for survey submissions. Every template writes to the one shared
/// <c>SUBMISSIONS</c> table — fixed base columns plus one nullable column per <c>data_name</c>,
/// separated by <c>TemplateId</c> — so submissions can be queried across templates without a
/// union. A column's SQL type is taken from the canonical <c>FIELD_CATALOG</c> entry, so a
/// <c>data_name</c> can never end up with two different types. All statements are loaded from an
/// editable SQL JSON file and executed via EF raw SQL. Intentionally outside the EF model
/// (no DbSet / entity / migration).
/// </summary>
public interface ISurveySubmissionStore
{
    /// <summary>
    /// Creates the shared submission table if missing and adds any column this template's fields
    /// still need. Only ever adds — never alters or drops, so historical values stay readable.
    /// </summary>
    Task ReconcileTableAsync(SurveyTemplate template, CancellationToken cancellationToken);

    /// <summary>Inserts one submission (answers keyed by <c>data_name</c>); returns the new row id.</summary>
    Task<long> InsertAsync(SubmissionInsert submission, CancellationToken cancellationToken);

    /// <summary>
    /// Overwrites the named answer columns on an existing row. Only the keys given are touched; every
    /// other column, and every base column, is left as it is.
    /// </summary>
    /// <remarks>
    /// Deliberately narrow, and not the way to record a change of answers. A survey filled again
    /// writes a <em>new</em> row — that is how the fill history and the per-role answer sets work,
    /// and rewriting an old row would erase both.
    ///
    /// This exists for the one case that cannot be expressed as a new fill: attaching media to a
    /// survey whose lifecycle has closed. <c>Survey.RecordFill</c> refuses an <c>APPROVED</c> survey,
    /// so a migration backfilling photos that reached the archive after the import has no row to
    /// write and must amend the one already there.
    /// </remarks>
    Task UpdateAnswersAsync(
        long templateId,
        long submissionId,
        IReadOnlyDictionary<string, object?> answers,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads a single submission row as a column-keyed dictionary, or null if not found.
    /// Projects base columns plus this template's own field columns.
    /// </summary>
    Task<IReadOnlyDictionary<string, object?>?> GetByIdAsync(long templateId, long submissionId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the most recent submission recorded against a survey, or null when it has none. This
    /// is what a repeat fill starts from — the survey is filled more than once, and each pass
    /// updates the answers rather than starting from a blank form.
    /// </summary>
    Task<IReadOnlyDictionary<string, object?>?> GetLatestBySurveyAsync(long templateId, long surveyId, CancellationToken cancellationToken);

    /// <summary>
    /// Every submission recorded against a survey, oldest first.
    /// </summary>
    /// <remarks>
    /// A survey is filled by both sides and each fill writes its own row, leaving the columns it did
    /// not answer NULL. No single row is therefore the survey's answer set — the field team's row
    /// and the back office's row each hold half of it. Callers that need the whole picture read them
    /// all and take the last non-null value per column.
    /// </remarks>
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ListBySurveyAsync(long templateId, long surveyId, CancellationToken cancellationToken);

    /// <summary>Server-side paged list of submission rows for a template.</summary>
    Task<(IReadOnlyList<IReadOnlyDictionary<string, object?>> Items, int Total)> ListAsync(long templateId, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// The id of the submission already recorded under this client key, or null when it is new.
    /// A mobile app that loses its connection mid-send retries the same queued fill; the key is how
    /// a retry is told apart from a second visit to the same survey.
    /// </summary>
    Task<long?> FindByClientIdAsync(long templateId, Guid clientSubmissionId, CancellationToken cancellationToken);

    /// <summary>
    /// The batch form of <see cref="FindByClientIdAsync"/>, keyed by client id — one round trip for a
    /// whole offline queue rather than one per item. Keys absent from the result are new.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, long>> FindByClientIdsAsync(
        long templateId,
        IReadOnlyCollection<Guid> clientSubmissionIds,
        CancellationToken cancellationToken);
}

/// <summary>
/// One row to write to the shared <c>SUBMISSIONS</c> table. <see cref="SurveyId"/> ties the fill to
/// the survey instance it answers — every fill answers one — and <see cref="FilledByRole"/> records
/// which side filled it: a survey accepts a field-team fill and a back-office fill against the same
/// instance.
/// </summary>
public sealed record SubmissionInsert
{
    public required long TemplateId { get; init; }
    public int? VersionNo { get; init; }
    public string? SubmittedBy { get; init; }
    public required long SurveyId { get; init; }
    public long? AssignmentId { get; init; }

    /// <summary>See <c>FilledByRoles</c>.</summary>
    public string? FilledByRole { get; init; }

    /// <summary>
    /// When the fill was made. Null — the normal case — stamps the row with the server clock, since
    /// a fill arriving now was made now. Set only by an importer of historical data, where stamping
    /// hundreds of records with the moment the import ran would replace the one date that says when
    /// the field work was actually done.
    /// </summary>
    public DateTimeOffset? SubmittedDate { get; init; }

    /// <summary>
    /// The client's own key for this fill, when it has one. A mobile app generates it once, when the
    /// crew saves the form on the device, and sends the same value on every replay attempt — a
    /// unique index on it is what stops a retried sync writing the fill twice. Null for a fill
    /// entered directly against the API, which has no queue to replay.
    /// </summary>
    public Guid? ClientSubmissionId { get; init; }

    /// <summary>Answers keyed by field <c>data_name</c>. Unknown keys are ignored.</summary>
    public required IReadOnlyDictionary<string, object?> Answers { get; init; }
}
