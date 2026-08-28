using System.Linq.Expressions;

namespace KH.Application.Common.Interfaces;

/// <summary>
/// Hands work to a background processor so a request does not wait on it. Kept as an abstraction
/// because the Application layer carries no reference to the job library — the implementation in
/// Infrastructure is the only thing that knows Hangfire exists.
/// </summary>
public interface IBackgroundJobScheduler
{
    /// <summary>
    /// Queues <paramref name="job"/> to run out of band. <typeparamref name="TJob"/> is resolved from
    /// DI when the job runs, so the expression must name a registered service and carry only
    /// serializable arguments.
    /// </summary>
    /// <returns>
    /// <c>false</c> when no job processor is available and the work was therefore not queued. Callers
    /// are expected to treat that as a skipped optional step, not a failure.
    /// </returns>
    bool Enqueue<TJob>(Expression<Func<TJob, Task>> job) where TJob : class;
}
