namespace KH.Application.Fsms.DataMigration.Jobs;

/// <summary>
/// Works through one queued import run. Out of band because a run reads a whole export, writes a
/// survey and a submission per record and copies every referenced photo into storage — minutes of
/// work that no request should be held open for.
/// </summary>
public interface IDataMigrationJob
{
    /// <summary>
    /// Imports the run's file. Safe to re-queue: a record whose survey already exists is skipped, so
    /// a run resumed after a crash finishes what the first attempt left rather than duplicating it.
    /// A run already past <c>PENDING</c> is left alone.
    /// </summary>
    /// <param name="cancellationToken">Supplied by the job processor when it shuts down mid-run.</param>
    Task RunAsync(long runId, CancellationToken cancellationToken = default);
}
