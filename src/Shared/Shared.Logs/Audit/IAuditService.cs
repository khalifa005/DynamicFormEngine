using System.Threading.Tasks;
using Hangfire;
using Shared.Core.Entities;

namespace Shared.Logs.Audit;

/// <summary>
/// Interface for persisting request-response audit log records.
/// Contains the method intended to be executed asynchronously as a Hangfire background job.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Saves the audit entry asynchronously.
    /// This method is designated as a Hangfire background job running in a dedicated 'audit' queue.
    /// </summary>
    /// <param name="entry">A plain, serialization-safe AuditEntry record.</param>
    [Queue("audit")]
    [AutomaticRetry(
        Attempts = 3, 
        LogEvents = true, 
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    Task SaveAsync(AuditEntry entry);
}
