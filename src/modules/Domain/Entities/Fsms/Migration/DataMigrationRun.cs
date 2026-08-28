using KH.Domain.Constants.Fsms;

namespace KH.Domain.Entities.Fsms.Migration;

/// <summary>
/// Aggregate root for one import of an external data set — the operator picked a source, uploaded a
/// file and pressed go. Queued as <c>PENDING</c> by the request, worked through by a background job
/// (<c>RUNNING</c>) and closed as <c>COMPLETED</c> or <c>FAILED</c>. Table: <c>DATA_MIGRATION_RUNS</c>.
///
/// The counters exist because the run is the only thing the operator can see: the job writes
/// hundreds of surveys over several minutes, and a page that could only say "still going" would be
/// no better than watching the database. They are flushed per batch, so the progress shown moves
/// while the run is in flight rather than all at once at the end.
///
/// A failure of one record is not a failure of the run — see <see cref="FailedCount"/>. Only
/// something that stops the whole file being processed sets <see cref="MigrationRunStatuses.Failed"/>.
/// </summary>
public sealed class DataMigrationRun : BaseAuditableEntity<long>
{
    private readonly List<DataMigrationRecord> _records = [];

    private DataMigrationRun()
    {
    }

    private DataMigrationRun(
        string sourceCode,
        string mode,
        long templateId,
        string fileName,
        string storedPath,
        string? optionsJson,
        string? requestedBy)
    {
        SourceCode = sourceCode;
        Mode = mode;
        TemplateId = templateId;
        FileName = fileName;
        StoredPath = storedPath;
        OptionsJson = optionsJson;
        RequestedBy = requestedBy;
        Status = MigrationRunStatuses.Pending;
        IsActive = true;
    }

    /// <summary>See <see cref="MigrationSourceCodes"/>.</summary>
    public string SourceCode { get; private set; } = default!;

    /// <summary>See <see cref="MigrationModes"/>.</summary>
    public string Mode { get; private set; } = default!;

    /// <summary>See <see cref="MigrationRunStatuses"/>.</summary>
    public string Status { get; private set; } = default!;

    /// <summary>
    /// The published template every survey in this run is raised against, chosen by the operator.
    /// One source exports many different forms, so the source cannot decide this.
    /// </summary>
    public long TemplateId { get; private set; }

    /// <summary>
    /// Columns in the file that no field of the chosen template claims, as a readable list.
    ///
    /// Recorded rather than merely logged because the template is the operator's choice: picking the
    /// wrong one is the easiest mistake to make on this screen, and a file whose columns match
    /// nothing is exactly what that looks like. A validate run that reports forty unmapped columns
    /// has told the operator what went wrong before a single row is written.
    /// </summary>
    public string? UnmappedColumns { get; private set; }

    /// <summary>How many of the file's columns did land on a field. Read next to the above.</summary>
    public int MappedColumnCount { get; private set; }

    /// <summary>
    /// Every column in the file. Without it "37 columns matched" is unreadable — 37 of 40 is a
    /// healthy import and 37 of 400 is the wrong template, and the operator cannot tell which.
    /// </summary>
    public int SourceColumnCount { get; private set; }

    /// <summary>How many columns are named in <see cref="UnmappedColumns"/>.</summary>
    public int UnmappedColumnCount { get; private set; }

    /// <summary>
    /// Columns the source adapter passed over deliberately — the export's own bookkeeping and the
    /// sidecar columns beside each media field — as a readable list.
    ///
    /// Named rather than counted for the same reason as <see cref="UnmappedColumns"/>: "43 ignored"
    /// asks the operator to take on trust that all forty-three were noise, and the only way to answer
    /// that is to read them. A column the adapter skips that should in fact have been imported turns
    /// up here and nowhere else, because the adapter applies its own exclusions before the template
    /// is consulted — so this list is the only place that mistake is visible.
    /// </summary>
    public string? IgnoredColumns { get; private set; }

    /// <summary>How many columns are named in <see cref="IgnoredColumns"/>.</summary>
    public int IgnoredColumnCount { get; private set; }

    /// <summary>The name the operator's file arrived under; shown in the run list.</summary>
    public string FileName { get; private set; } = default!;

    /// <summary>
    /// Where the uploaded workbook was parked, storage-root-relative. The job runs long after the
    /// request that carried the bytes has ended, so the file has to outlive it.
    /// </summary>
    public string StoredPath { get; private set; } = default!;

    /// <summary>
    /// The org placement and optional field team the operator chose, as JSON. Kept whole rather than
    /// spread over columns: it is the run's input record, only ever read back as a unit, and a later
    /// source may need to remember different choices.
    /// </summary>
    public string? OptionsJson { get; private set; }

    public string? RequestedBy { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Records read from the file. Zero until the job has parsed it.</summary>
    public int TotalRecords { get; private set; }

    public int ImportedCount { get; private set; }

    /// <summary>Records an earlier run had already written. Expected on a re-run, not a problem.</summary>
    public int SkippedCount { get; private set; }

    /// <summary>Records that could not be written. The run still completes; see the record rows.</summary>
    public int FailedCount { get; private set; }

    /// <summary>Records read and checked but deliberately not written — a validate-only run.</summary>
    public int ValidatedCount { get; private set; }

    /// <summary>
    /// Records dealt with, whatever the outcome. This is what progress is measured against, so a
    /// validate run — whose records are all <c>VALIDATED</c> — reports progress like any other.
    /// </summary>
    public int ProcessedCount => ImportedCount + SkippedCount + FailedCount + ValidatedCount;

    /// <summary>Media files attached, or — on a validate run — found in the archive.</summary>
    public int FilesImported { get; private set; }

    /// <summary>
    /// Media a record referenced that was not on disk. Counted rather than fatal: an export whose
    /// photo folder is short a few files still carries answers worth keeping, and the count is what
    /// tells the operator whether that is a handful or the whole folder.
    /// </summary>
    public int FilesMissing { get; private set; }

    /// <summary>Why the run itself stopped. Null unless <see cref="Status"/> is <c>FAILED</c>.</summary>
    public string? ErrorMessage { get; private set; }

    public IReadOnlyCollection<DataMigrationRecord> Records => _records.AsReadOnly();

    public static DataMigrationRun Create(
        string sourceCode,
        string mode,
        long templateId,
        string fileName,
        string storedPath,
        string? optionsJson,
        string? requestedBy)
    {
        if (!MigrationSourceCodes.IsDefined(sourceCode))
        {
            throw new DomainException($"Unknown migration source '{sourceCode}'.");
        }

        if (!MigrationModes.IsDefined(mode))
        {
            throw new DomainException($"Unknown migration mode '{mode}'.");
        }

        if (templateId <= 0)
        {
            throw new DomainException("A migration run must name the template its surveys are raised against.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainException("A migration run must name the uploaded file.");
        }

        if (string.IsNullOrWhiteSpace(storedPath))
        {
            throw new DomainException("A migration run must record where the uploaded file was stored.");
        }

        return new DataMigrationRun(
            sourceCode,
            mode,
            templateId,
            fileName.Trim(),
            storedPath.Trim(),
            optionsJson,
            requestedBy);
    }

    /// <summary>
    /// Records how well the file's columns matched the chosen template. Set once the file has been
    /// parsed, alongside <see cref="Start"/>.
    /// </summary>
    public void RecordColumnMatch(
        int sourceColumnCount,
        int mappedColumnCount,
        IReadOnlyList<string> unmappedColumns,
        IReadOnlyList<string> ignoredColumns)
    {
        SourceColumnCount = sourceColumnCount;
        MappedColumnCount = mappedColumnCount;

        UnmappedColumnCount = unmappedColumns.Count;
        UnmappedColumns = Join(unmappedColumns);

        IgnoredColumnCount = ignoredColumns.Count;
        IgnoredColumns = Join(ignoredColumns);
    }

    private static string? Join(IReadOnlyList<string> columns) =>
        columns.Count == 0 ? null : string.Join(", ", columns);

    /// <summary>
    /// The job has picked the run up and knows how many records the file holds. Refused on a run
    /// already past <c>PENDING</c>, so a job re-queued after a crash cannot restart a finished run
    /// and zero its counters.
    /// </summary>
    public void Start(DateTimeOffset startedAt, int totalRecords)
    {
        if (Status != MigrationRunStatuses.Pending)
        {
            throw new DomainException($"A {Status} migration run cannot be started.");
        }

        if (totalRecords < 0)
        {
            throw new DomainException("The record count cannot be negative.");
        }

        Status = MigrationRunStatuses.Running;
        StartedAt = startedAt;
        TotalRecords = totalRecords;
    }

    /// <summary>
    /// Records one record's outcome and folds it into the run's tallies.
    ///
    /// The counters are derived from the record rather than passed alongside it, deliberately. They
    /// used to be a separate call taking five positional integers, and a validate run — whose records
    /// are none of imported, skipped or failed — incremented nothing, so a run that had processed
    /// every row reported zero progress. Deriving them here means a new outcome cannot be added
    /// without the tallies following it.
    ///
    /// Called per record rather than at the end, which is what lets the page show a progress bar that
    /// climbs on a run still in flight.
    /// </summary>
    public DataMigrationRecord AddRecord(DataMigrationRecord record)
    {
        _records.Add(record);

        switch (record.Status)
        {
            case MigrationRecordStatuses.Imported:
                ImportedCount++;
                break;
            case MigrationRecordStatuses.Skipped:
                SkippedCount++;
                break;
            case MigrationRecordStatuses.Failed:
                FailedCount++;
                break;
            case MigrationRecordStatuses.Validated:
                ValidatedCount++;
                break;
            default:
                throw new DomainException($"Unknown migration record status '{record.Status}'.");
        }

        FilesImported += record.FilesImported;
        FilesMissing += record.FilesMissing;

        return record;
    }

    public void Complete(DateTimeOffset completedAt)
    {
        if (Status != MigrationRunStatuses.Running)
        {
            throw new DomainException($"A {Status} migration run cannot be completed.");
        }

        Status = MigrationRunStatuses.Completed;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Stops the run outright. Allowed from <c>PENDING</c> as well as <c>RUNNING</c>: the file may
    /// turn out to be unreadable before a single record has been parsed, and that run has to land
    /// somewhere the operator can see rather than sit queued forever.
    /// </summary>
    public void Fail(DateTimeOffset failedAt, string message)
    {
        if (MigrationRunStatuses.IsTerminal(Status))
        {
            throw new DomainException($"A {Status} migration run cannot be failed.");
        }

        Status = MigrationRunStatuses.Failed;
        CompletedAt = failedAt;
        ErrorMessage = string.IsNullOrWhiteSpace(message) ? "The migration run failed." : message.Trim();
    }

}
