using System.Linq.Expressions;
using Hangfire;
using KH.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KH.Infrastructure.Services.Jobs;

/// <summary>
/// Hangfire-backed <see cref="IBackgroundJobScheduler"/>.
///
/// <see cref="IBackgroundJobClient"/> is resolved from the provider rather than injected, because it
/// is only registered when <c>Hangfire:Enabled</c> is true — constructor injection would make every
/// consumer of this scheduler unresolvable on a deployment that runs with Hangfire off. The same
/// approach is taken by the audit-logging filter for the same reason.
/// </summary>
public sealed class HangfireBackgroundJobScheduler(
    IServiceProvider serviceProvider,
    ILogger<HangfireBackgroundJobScheduler> logger)
    : IBackgroundJobScheduler
{
    public bool Enqueue<TJob>(Expression<Func<TJob, Task>> job) where TJob : class
    {
        var client = serviceProvider.GetService<IBackgroundJobClient>();

        if (client is null)
        {
            logger.LogWarning(
                "Background job for {JobType} was not queued: Hangfire is disabled (Hangfire:Enabled), so no job client is registered.",
                typeof(TJob).Name);

            return false;
        }

        try
        {
            var jobId = client.Enqueue(job);

            logger.LogInformation(
                "Queued background job {JobId} for {JobType}.", jobId, typeof(TJob).Name);

            return true;
        }
        catch (Exception ex)
        {
            // Queuing is an optional follow-up to whatever the caller just committed; a broker or
            // storage fault must not roll that back or surface as a failed request.
            logger.LogError(
                ex, "Failed to queue background job for {JobType}.", typeof(TJob).Name);

            return false;
        }
    }
}
