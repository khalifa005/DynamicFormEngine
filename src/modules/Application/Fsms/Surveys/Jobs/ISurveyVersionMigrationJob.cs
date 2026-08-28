namespace KH.Application.Fsms.Surveys.Jobs;

/// <summary>
/// Background fan-out behind a template's auto-migrate flag: re-pins the template's own unfilled
/// surveys onto a newly published version. Run out of band because a template with a large open
/// worklist would otherwise make publishing wait on every one of its surveys.
/// </summary>
public interface ISurveyVersionMigrationJob
{
    /// <summary>
    /// Moves every eligible survey of <paramref name="templateId"/> onto version
    /// <paramref name="targetVersionNo"/>. Safe to run more than once — surveys already on that
    /// version or newer are filtered out.
    /// </summary>
    /// <param name="requestedBy">The user whose publish triggered this; recorded in status history.</param>
    /// <param name="cancellationToken">Supplied by the job processor when it shuts down mid-run.</param>
    Task MigrateTemplateSurveysAsync(
        long templateId,
        int targetVersionNo,
        string? requestedBy,
        CancellationToken cancellationToken = default);
}
