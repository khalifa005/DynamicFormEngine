namespace KH.Application.Fsms.DataMigration.Models;

/// <summary>One import run as the run grid shows it.</summary>
public sealed record MigrationRunListItemDto
{
    public long Id { get; init; }
    public string SourceCode { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long TemplateId { get; init; }
    public string? TemplateCode { get; init; }
    public string? TemplateName { get; init; }

    /// <summary>
    /// Columns the chosen template did not claim, and how many it did. Read together they are the
    /// answer to "did I pick the right template?" — the easiest mistake to make on this screen.
    /// </summary>
    public string? UnmappedColumns { get; init; }

    public int MappedColumnCount { get; init; }
    public int SourceColumnCount { get; init; }
    public int UnmappedColumnCount { get; init; }

    /// <summary>
    /// Columns the importer knowingly passes over — a source's own bookkeeping, and the caption/url
    /// sidecars beside each media column. Named rather than counted, because a count alone asks the
    /// operator to trust that every one of them was noise. Null on runs made before the names were
    /// kept; only a fresh run can recover them.
    /// </summary>
    public string? IgnoredColumns { get; init; }

    /// <summary>
    /// Recorded by the run, but falling back to the arithmetic for rows written before the count was
    /// stored — the three buckets partition the file's columns, so the remainder is exactly this.
    /// Without the fallback, upgrading would silently zero the number on every existing run.
    /// </summary>
    public int IgnoredColumnCount =>
        StoredIgnoredColumnCount > 0
            ? StoredIgnoredColumnCount
            : Math.Max(0, SourceColumnCount - MappedColumnCount - UnmappedColumnCount);

    public int StoredIgnoredColumnCount { get; init; }

    public int TotalRecords { get; init; }
    public int ImportedCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }

    /// <summary>Records checked without being written — every record of a validate run.</summary>
    public int ValidatedCount { get; init; }

    public int FilesImported { get; init; }
    public int FilesMissing { get; init; }

    public string? RequestedBy { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>Records dealt with, whatever the outcome — what progress is measured against.</summary>
    public int ProcessedCount => ImportedCount + SkippedCount + FailedCount + ValidatedCount;

    /// <summary>
    /// How far the run has got, 0–100. Computed here rather than in the client so the run grid, the
    /// detail page and anything added later all read the same number.
    /// </summary>
    public int ProgressPercent => TotalRecords <= 0
        ? 0
        : (int)Math.Round(ProcessedCount * 100d / TotalRecords);

    /// <summary>True once the run no longer moves, so the client can stop polling.</summary>
    public bool IsTerminal { get; init; }

    /// <summary>
    /// How long the job spent on the file, in seconds — start to finish while it is still running.
    ///
    /// Null until the job picks the run up. That gap is meaningful in itself: a run that has been
    /// queued for a while with no <c>StartedAt</c> is waiting on the background processor, not
    /// working slowly, and those two need different responses from the operator.
    /// </summary>
    public double? DurationSeconds => StartedAt is null
        ? null
        : ((CompletedAt ?? DateTimeOffset.UtcNow) - StartedAt.Value).TotalSeconds;

    /// <summary>
    /// Seconds still to go at the rate achieved so far, or null when there is nothing to project
    /// from. Deliberately naive — records cost roughly the same each, so a flat average is honest
    /// enough for "another minute or so" and cheaper than pretending to more precision.
    /// </summary>
    public double? EstimatedSecondsRemaining
    {
        get
        {
            if (IsTerminal || DurationSeconds is not double elapsed || ProcessedCount <= 0)
            {
                return null;
            }

            var remaining = TotalRecords - ProcessedCount;
            return remaining <= 0 ? 0 : remaining * (elapsed / ProcessedCount);
        }
    }
}

/// <summary>What became of one record, as the run's detail table shows it.</summary>
public sealed record MigrationRunRecordDto
{
    public long Id { get; init; }
    public int RowNumber { get; init; }
    public string? ExternalId { get; init; }
    public string Status { get; init; } = string.Empty;
    public long? SurveyId { get; init; }
    public string? SurveyCode { get; init; }
    public long? SubmissionId { get; init; }
    public int FilesImported { get; init; }
    public int FilesMissing { get; init; }
    public string? Message { get; init; }
}
