using KH.Application.Common.Interfaces;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Logging;
using Shared.Core.Exceptions;

namespace KH.Application.Fsms.Surveys.Jobs;

/// <summary>
/// Re-pins a template's unfilled surveys onto a newly published version. Deliberately free of
/// Hangfire types: it is an ordinary registered service, and <see cref="IBackgroundJobScheduler"/>
/// is what knows how to run it out of band.
/// </summary>
public sealed class SurveyVersionMigrationJob(
    IApplicationDbContext context,
    TimeProvider timeProvider,
    ILogger<SurveyVersionMigrationJob> logger)
    : ISurveyVersionMigrationJob
{
    /// <summary>
    /// Surveys are loaded and saved in chunks so a template with a large open worklist does not hold
    /// one enormous transaction, and so a failure part-way leaves the earlier batches migrated.
    /// </summary>
    private const int BatchSize = 200;

    public async Task MigrateTemplateSurveysAsync(
        long templateId,
        int targetVersionNo,
        string? requestedBy,
        CancellationToken cancellationToken = default)
    {
        var targetVersionId = await context.TemplateVersions
            .AsNoTracking()
            .Where(v => v.TemplateId == templateId
                && v.VersionNo == targetVersionNo
                && v.TargetClient == TargetClients.Formly)
            .Select(v => (long?)v.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (targetVersionId is not long versionId)
        {
            // The version was rolled back or never written. Nothing to migrate onto.
            logger.LogWarning(
                "Auto-migration skipped for template {TemplateId}: no {TargetClient} snapshot exists at version {VersionNo}.",
                templateId,
                TargetClients.Formly,
                targetVersionNo);

            return;
        }

        var candidateIds = await context.Surveys
            .AsNoTracking()
            .Where(s => s.TemplateId == templateId
                && (s.TemplateVersionNo == null || s.TemplateVersionNo < targetVersionNo)
                && SurveyStatuses.AutoMigratable.Contains(s.Status))
            .OrderBy(s => s.Id)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
        {
            logger.LogInformation(
                "Auto-migration for template {TemplateId} to version {VersionNo} found no eligible surveys.",
                templateId,
                targetVersionNo);

            return;
        }

        var note = $"Automatically moved to template version {targetVersionNo} on publish.";
        var migrated = 0;
        var skipped = 0;

        foreach (var batch in candidateIds.Chunk(BatchSize))
        {
            var surveys = await context.Surveys
                .Include(s => s.StatusHistory)
                .Where(s => batch.Contains(s.Id))
                .ToListAsync(cancellationToken);

            foreach (var survey in surveys)
            {
                try
                {
                    survey.MigrateToTemplateVersion(
                        versionId,
                        targetVersionNo,
                        requestedBy,
                        timeProvider.GetUtcNow(),
                        note);

                    migrated++;
                }
                catch (DomainException ex)
                {
                    // A survey that changed state between the id sweep and now no longer qualifies.
                    // It is the one survey's problem, not the batch's.
                    skipped++;

                    logger.LogInformation(
                        "Survey {SurveyId} was not moved to template version {VersionNo}: {Reason}",
                        survey.Id,
                        targetVersionNo,
                        ex.Message);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Auto-migration for template {TemplateId} to version {VersionNo} completed: {Migrated} moved, {Skipped} skipped.",
            templateId,
            targetVersionNo,
            migrated,
            skipped);
    }
}
