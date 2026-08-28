using System;
using System.Threading.Tasks;
using Hangfire;
using KH.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Shared.Logs.Audit;
using Shared.Core.Entities;

namespace KH.Infrastructure.Services;

/// <summary>
/// Service responsible for persisting request-response audit log records.
/// Performs database writes asynchronously as a Hangfire background job using the primary application DbContext.
/// </summary>
public class AuditService : IAuditService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<AuditService> _logger;

    public AuditService(IApplicationDbContext dbContext, ILogger<AuditService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Saves the audit entry asynchronously using the primary EF Core DbContext.
    /// Run as a Hangfire background job on the isolated "audit" queue.
    /// </summary>
    /// <param name="entry">The plain, serializable AuditEntry to save.</param>
    [Queue("audit")]
    [AutomaticRetry(
        Attempts = 3, 
        LogEvents = true, 
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task SaveAsync(AuditEntry entry)
    {
        if (entry == null)
        {
            _logger.LogWarning("SaveAsync called with a null AuditEntry. Skipping database write.");
            return;
        }

        try
        {
            _dbContext.AuditLogs.Add(entry);
            await _dbContext.SaveChangesAsync(System.Threading.CancellationToken.None);
            _logger.LogDebug("Audit log successfully saved to primary database context for CorrelationId: {CorrelationId}", entry.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist AuditEntry to primary database context. CorrelationId: {CorrelationId}", entry.CorrelationId);
            // Re-throw so that Hangfire detects the exception and triggers the retry mechanism.
            throw;
        }
    }
}
