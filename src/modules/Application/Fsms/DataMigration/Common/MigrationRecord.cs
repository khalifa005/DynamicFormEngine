namespace KH.Application.Fsms.DataMigration.Common;

/// <summary>
/// One record of an external data set, in the shape the import pipeline understands. Every adapter
/// flattens its own file format down to this, which is what keeps the job that writes surveys from
/// knowing anything about Fulcrum, Microsoft Forms or whatever comes next.
/// </summary>
public sealed record MigrationRecord
{
    /// <summary>
    /// The source system's own key. Doubles as the idempotency key: the survey code is derived from
    /// it, so re-running the same file finds the surveys it already wrote instead of writing them
    /// again.
    /// </summary>
    public required string ExternalId { get; init; }

    /// <summary>1-based row in the source file, reported back so a failure can be located in it.</summary>
    public required int RowNumber { get; init; }

    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    /// <summary>When the record was collected in the field, per the source system.</summary>
    public DateTimeOffset? CapturedAt { get; init; }

    /// <summary>Who collected it, per the source system. Free text — not one of our logins.</summary>
    public string? CapturedBy { get; init; }

    /// <summary>
    /// The source system's own status word, as written. Mapped to one of our survey statuses by the
    /// adapter through <see cref="IsApproved"/> rather than compared here, since each source spells
    /// "finished" differently.
    /// </summary>
    public string? SourceStatus { get; init; }

    /// <summary>
    /// True when the source considered this record closed, so the imported survey is completed
    /// rather than left awaiting review. Decided by the adapter — it is the only thing that knows
    /// what its source's status words mean.
    /// </summary>
    public bool IsApproved { get; init; }

    /// <summary>Non-media answers keyed by our field <c>data_name</c>.</summary>
    public IReadOnlyDictionary<string, object?> Answers { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Media answers keyed by <c>data_name</c>, each holding the source's own file keys. The bytes
    /// are not here: they sit in the export's media folder and are resolved by key at import time,
    /// so a workbook stays a workbook rather than carrying several gigabytes of photos.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Media { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Everything the source carried that our template does not model, kept verbatim on the survey's
    /// <c>AdditionalDataJson</c>. Nothing is thrown away just because there is no field for it.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Extras { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Why the adapter could not read this row. Non-null means the row is unusable.</summary>
    public string? ParseError { get; init; }

    /// <summary>Total media keys across every media field — what the run's file counters are measured against.</summary>
    public int MediaCount => Media.Values.Sum(keys => keys.Count);
}
