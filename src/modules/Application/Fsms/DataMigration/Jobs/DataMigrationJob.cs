using System.Text.Json;
using KH.Application.Common.Interfaces;
using KH.Application.Common.Options;
using KH.Application.Fsms.Common.Definition;
using KH.Application.Fsms.DataMigration.Common;
using KH.Application.Fsms.Submissions.Common;
using KH.Application.Fsms.Submissions.Interfaces;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Migration;
using KH.Domain.Entities.Fsms.Surveys;
using KH.Domain.Entities.Fsms.Templates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Core.Exceptions;

namespace KH.Application.Fsms.DataMigration.Jobs;

/// <summary>
/// Works through one import run: read the file, then per record raise a survey, record the fill it
/// arrived with and copy its media in.
///
/// Two things shape the whole of this class. First, an imported record is a survey that was already
/// carried out, so it is walked through the same lifecycle a live one is — created, optionally
/// allocated, filled, and completed when the source says it was closed. Writing a status straight
/// into the row instead would leave surveys with no history and no assignment trail, and the
/// worklist reads both.
///
/// Second, the source data is history and is imported as it stands: <c>SurveyAnswerValidator</c> is
/// deliberately not run. A rule added to our template long after the field work cannot make that
/// field work wrong, and refusing records over it would leave the operator with an import they
/// cannot complete and no way to see what is in it. The run report is where problems surface.
/// </summary>
public sealed class DataMigrationJob(
    IApplicationDbContext context,
    IMigrationSourceRegistry sourceRegistry,
    ISurveySubmissionStore submissionStore,
    IFileStorage fileStorage,
    TimeProvider timeProvider,
    IOptions<DataMigrationOptions> options,
    ILogger<DataMigrationJob> logger)
    : IDataMigrationJob
{
    /// <summary>Fulcrum's meter serial maps onto the survey's own column so the worklist can filter on it.</summary>
    private const string MeterNumberDataName = "water_meter_serial_number";

    /// <summary>Likewise the census number (رقم الحصر).</summary>
    private const string HcnDataName = "hcn_number";

    private static readonly JsonSerializerOptions ExtrasSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task RunAsync(long runId, CancellationToken cancellationToken = default)
    {
        var run = await context.DataMigrationRuns
            .FirstOrDefaultAsync(x => x.Id == runId, cancellationToken);

        if (run is null)
        {
            logger.LogWarning("Migration run {RunId} no longer exists; nothing to import.", runId);
            return;
        }

        // A processor that re-delivered the job after a restart must not restart a run that already
        // finished. Start() refuses anything past PENDING; the guard keeps a duplicate delivery out
        // of the logs as an exception when it is really just a duplicate delivery.
        if (run.Status != MigrationRunStatuses.Pending)
        {
            logger.LogInformation("Migration run {RunId} is already {Status}; skipping.", runId, run.Status);
            return;
        }

        try
        {
            await ImportAsync(run, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The processor is shutting down. Recorded rather than swallowed: the records already
            // committed are real, and re-queueing the run picks up the rest by skipping them.
            await FailAsync(run, "The import was cancelled before it finished.");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Migration run {RunId} failed.", runId);
            await FailAsync(run, exception.Message);
        }
    }

    private async Task ImportAsync(DataMigrationRun run, CancellationToken cancellationToken)
    {
        var adapter = sourceRegistry.Find(run.SourceCode);
        if (adapter is null)
        {
            await FailAsync(run, $"No importer is registered for source '{run.SourceCode}'.");
            return;
        }

        var template = await context.SurveyTemplates
            .FirstOrDefaultAsync(x => x.Id == run.TemplateId, cancellationToken);

        if (template is null)
        {
            await FailAsync(run, "The template this run was raised against no longer exists.");
            return;
        }

        var definition = SurveyDefinitionParser.Parse(template.DefinitionJson);
        if (definition.Fields.Count == 0)
        {
            await FailAsync(run, "The template has no fields to import into.");
            return;
        }

        MigrationParseResult parsed;

        try
        {
            await using var source = await fileStorage.OpenReadAsync(run.StoredPath, cancellationToken);
            parsed = adapter.Parse(source, new MigrationParseContext(definition), cancellationToken);
        }
        catch (MigrationParseException exception)
        {
            await FailAsync(run, exception.Message);
            return;
        }
        catch (FileNotFoundException)
        {
            // Names the machine deliberately. The usual cause is not a deleted file: it is that two
            // Hangfire servers share one job queue — every process pointed at the same database
            // polls it — so the job can be picked up by a machine that never received the upload.
            // Its storage root is its own, so the workbook is simply not there. Without the machine
            // name that failure is a mystery; with it, it names itself.
            await FailAsync(
                run,
                $"The uploaded file '{run.StoredPath}' was not found in storage on '{Environment.MachineName}'. "
                + "If more than one server shares this database, the job may have been picked up by a machine "
                + "other than the one the file was uploaded to.");
            return;
        }

        run.Start(timeProvider.GetUtcNow(), parsed.Records.Count);

        // Recorded on the run, not merely logged: the operator chooses the template, so "these forty
        // columns matched nothing" is the one place a wrong choice announces itself.
        run.RecordColumnMatch(
            parsed.SourceColumnCount, parsed.MappedColumnCount, parsed.UnmappedColumns, parsed.IgnoredColumns);
        await context.SaveChangesAsync(cancellationToken);

        if (parsed.MappedColumnCount == 0)
        {
            await FailAsync(
                run,
                "None of the file's columns match a field on the chosen template. This usually means the wrong template was selected for this export.");
            return;
        }

        var archive = new MigrationArchive(options.Value.ArchiveFolder, options.Value.AllowedExtensions);
        var existingCodes = await LoadExistingCodesAsync(run.SourceCode, parsed.Records, cancellationToken);

        if (run.Mode == MigrationModes.Validate)
        {
            await ValidateOnlyAsync(run, parsed, archive, existingCodes, cancellationToken);
            return;
        }

        if (run.Mode == MigrationModes.BackfillMedia)
        {
            await BackfillMediaAsync(run, parsed, template, archive, existingCodes, cancellationToken);
            return;
        }

        // DDL, so it runs once and outside every transaction below — adding a column inside one
        // would hold a schema lock for the length of that record's write.
        await submissionStore.ReconcileTableAsync(template, cancellationToken);

        var runOptions = MigrationRunOptions.FromJson(run.OptionsJson);
        var versionId = await ResolveVersionIdAsync(template, cancellationToken);

        foreach (var record in parsed.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ImportOneAsync(
                run, record, template, definition, versionId, runOptions, adapter.SourceCode, archive, existingCodes, cancellationToken);
        }

        run.Complete(timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Migration run {RunId} completed: {Imported} imported, {Skipped} skipped, {Failed} failed, {Files} file(s) copied, {Missing} missing.",
            run.Id,
            run.ImportedCount,
            run.SkippedCount,
            run.FailedCount,
            run.FilesImported,
            run.FilesMissing);
    }

    /// <summary>
    /// The validate-only pass: everything an import would check, nothing it would write. It walks
    /// the same media resolver, so a media root pointing at the wrong folder is found here rather
    /// than after several hundred surveys have been created around it.
    /// </summary>
    private async Task ValidateOnlyAsync(
        DataMigrationRun run,
        MigrationParseResult parsed,
        MigrationArchive archive,
        IReadOnlyDictionary<string, long> existingCodes,
        CancellationToken cancellationToken)
    {
        foreach (var record in parsed.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Reject(record) is string rejection)
            {
                run.AddRecord(DataMigrationRecord.Failed(record.RowNumber, record.ExternalId, rejection));
                continue;
            }

            var surveyCode = MigrationSurveyCode.For(run.SourceCode, record.ExternalId);

            if (existingCodes.TryGetValue(surveyCode, out var existingSurveyId))
            {
                run.AddRecord(DataMigrationRecord.Skipped(record.RowNumber, record.ExternalId, existingSurveyId, surveyCode));
                continue;
            }

            var media = await MigrationMediaImporter.ProbeAsync(fileStorage, archive, record, cancellationToken);

            run.AddRecord(DataMigrationRecord.Validated(
                record.RowNumber,
                record.ExternalId,
                media.Imported,
                media.MissingCount,
                media.MissingMessage()));

        }

        run.Complete(timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Migration run {RunId} validated {Total} record(s): {Skipped} already imported, {Failed} unreadable, {Files} media found, {Missing} missing.",
            run.Id,
            run.TotalRecords,
            run.SkippedCount,
            run.FailedCount,
            run.FilesImported,
            run.FilesMissing);
    }

    /// <summary>
    /// Attaches media that reached the archive after the import ran.
    ///
    /// This exists because the import's skip guard is the survey's existence, not its completeness —
    /// so a record imported while its photos were still elsewhere is `SKIPPED` by every later run,
    /// and its files would never arrive. Loosening that guard was the alternative and would have been
    /// worse: it is precisely what makes an interrupted import safe to resume.
    ///
    /// Answers and lifecycle are left alone. Nothing here calls <c>RecordFill</c>, which would be
    /// refused by the 923 surveys the import closed as <c>APPROVED</c> anyway, and would in any case
    /// claim a fill that never happened — a photo arriving late is not somebody filling the form again.
    /// </summary>
    private async Task BackfillMediaAsync(
        DataMigrationRun run,
        MigrationParseResult parsed,
        SurveyTemplate template,
        MigrationArchive archive,
        IReadOnlyDictionary<string, long> existingCodes,
        CancellationToken cancellationToken)
    {
        foreach (var record in parsed.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Reject(record) is string rejection)
            {
                run.AddRecord(DataMigrationRecord.Failed(record.RowNumber, record.ExternalId, rejection));
                await context.SaveChangesAsync(cancellationToken);
                continue;
            }

            var surveyCode = MigrationSurveyCode.For(run.SourceCode, record.ExternalId);

            // Skipped, not failed. A file being backfilled will hold rows that never imported for
            // unrelated reasons, and calling those failures here would bury the real ones.
            if (!existingCodes.TryGetValue(surveyCode, out var surveyId))
            {
                run.AddRecord(DataMigrationRecord.Skipped(
                    record.RowNumber,
                    record.ExternalId,
                    surveyId: null,
                    surveyCode: null,
                    "This record has not been imported yet, so there is no survey to attach media to. Run an import first."));

                await context.SaveChangesAsync(cancellationToken);
                continue;
            }

            if (record.Media.Count == 0)
            {
                run.AddRecord(DataMigrationRecord.Skipped(
                    record.RowNumber, record.ExternalId, surveyId, surveyCode, "The record references no media."));

                await context.SaveChangesAsync(cancellationToken);
                continue;
            }

            // The submission the import wrote, found by the same deterministic key it wrote it under.
            var submissionId = await submissionStore.FindByClientIdAsync(
                template.Id, MigrationClientKey.For(run.SourceCode, record.ExternalId), cancellationToken);

            if (submissionId is not long submission)
            {
                run.AddRecord(DataMigrationRecord.Skipped(
                    record.RowNumber,
                    record.ExternalId,
                    surveyId,
                    surveyCode,
                    "The survey exists but carries no imported submission, so there is nothing to attach media to."));

                await context.SaveChangesAsync(cancellationToken);
                continue;
            }

            var existingFiles = await context.SubmissionFiles
                .Where(file => file.SurveyId == surveyId && file.IsActive)
                .ToListAsync(cancellationToken);

            var media = await MigrationMediaImporter.BackfillAsync(
                context, fileStorage, archive, record, template.Id, surveyId, submission, existingFiles, cancellationToken);

            // No transaction on this path, and none was opened above: `BackfillAsync` only ever adds a
            // file row on the same step that increments the count, so nothing attached means nothing
            // tracked and nothing to roll back. Most records of a backfill run take this branch, and
            // a transaction per record to protect no writes would be several hundred for nothing.
            if (media.Attached == 0)
            {
                run.AddRecord(DataMigrationRecord.Skipped(
                    record.RowNumber,
                    record.ExternalId,
                    surveyId,
                    surveyCode,
                    media.MissingCount > 0
                        ? media.MissingMessage()
                        : "Every file this record references is already attached."));

                await context.SaveChangesAsync(cancellationToken);
                continue;
            }

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            // The file rows first: the answer references their ids, so a rewritten answer pointing at
            // rows that failed to save would leave the survey naming files that do not exist. The
            // store's command enlists in this transaction, so the two land together or not at all.
            await context.SaveChangesAsync(cancellationToken);

            await submissionStore.UpdateAnswersAsync(template.Id, submission, media.Answers, cancellationToken);

            run.AddRecord(DataMigrationRecord.Imported(
                record.RowNumber,
                record.ExternalId,
                surveyId,
                surveyCode,
                submission,
                media.Attached,
                media.MissingCount,
                media.MissingMessage()));

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        run.Complete(timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Migration run {RunId} backfilled media for {Total} record(s): {Attached} file(s) attached, {Missing} still missing, {Skipped} record(s) had nothing to do.",
            run.Id,
            run.TotalRecords,
            run.FilesImported,
            run.FilesMissing,
            run.SkippedCount);
    }

    /// <summary>
    /// One record, in one transaction. Per record rather than per batch on purpose: the submission
    /// store writes through native SQL, which commits as it is issued, so a transaction spanning it
    /// and EF is the only thing that keeps a half-written record from leaving submission rows whose
    /// survey never advanced — and those rows are indistinguishable from accepted work on the next
    /// run. Scoping it to a single record also means a rollback never discards work that succeeded.
    /// </summary>
    private async Task ImportOneAsync(
        DataMigrationRun run,
        MigrationRecord record,
        SurveyTemplate template,
        SurveyDefinition definition,
        long? versionId,
        MigrationRunOptions runOptions,
        string sourceCode,
        MigrationArchive archive,
        IReadOnlyDictionary<string, long> existingCodes,
        CancellationToken cancellationToken)
    {
        if (Reject(record) is string rejection)
        {
            run.AddRecord(DataMigrationRecord.Failed(record.RowNumber, record.ExternalId, rejection));
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        var surveyCode = MigrationSurveyCode.For(sourceCode, record.ExternalId);

        if (existingCodes.TryGetValue(surveyCode, out var existingSurveyId))
        {
            run.AddRecord(DataMigrationRecord.Skipped(record.RowNumber, record.ExternalId, existingSurveyId, surveyCode));
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        var now = timeProvider.GetUtcNow();
        var capturedAt = record.CapturedAt ?? now;
        var answers = SurveyAnswerKeys.Normalize(record.Answers);

        Survey survey;

        // Built before anything is written, so a record the domain refuses costs a row in the report
        // and nothing else — no partial write to roll back, and no orphaned entity left tracked.
        try
        {
            survey = BuildSurvey(record, surveyCode, template, versionId, runOptions, sourceCode, answers, capturedAt, run.RequestedBy);
        }
        catch (DomainException exception)
        {
            run.AddRecord(DataMigrationRecord.Failed(record.RowNumber, record.ExternalId, exception.Message));
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        context.Surveys.Add(survey);

        // Saved first because both the media rows and the submission need the survey's id; the
        // enclosing transaction is what keeps the three together.
        await context.SaveChangesAsync(cancellationToken);

        long? assignmentId = null;

        if (runOptions.FieldTeamId is long fieldTeamId)
        {
            // Before the fill: Assign refuses a survey that already holds submissions, since handing
            // one on would leave collected answers attributed to a crew that no longer has the work.
            var assignment = survey.Assign(fieldTeamId, run.RequestedBy, capturedAt, dueDate: null, $"Imported from {sourceCode}.");
            await context.SaveChangesAsync(cancellationToken);
            assignmentId = assignment.Id;
        }

        var media = await MigrationMediaImporter.ImportAsync(
            context,
            fileStorage,
            archive,
            record,
            template.Id,
            survey.Id,
            answers,
            cancellationToken);

        var submissionId = await submissionStore.InsertAsync(
            new SubmissionInsert
            {
                TemplateId = template.Id,
                VersionNo = survey.TemplateVersionNo ?? template.CurrentVersionNo,

                // The crew that collected the record, per the source — not one of our logins. That
                // is the point: the row should say who did the work, not who ran the import.
                SubmittedBy = record.CapturedBy ?? run.RequestedBy,
                SubmittedDate = capturedAt,
                SurveyId = survey.Id,
                AssignmentId = assignmentId,
                FilledByRole = FilledByRoles.FieldTeam,
                ClientSubmissionId = MigrationClientKey.For(sourceCode, record.ExternalId),
                Answers = answers,
            },
            cancellationToken);

        // Reused rather than linked by hand: which files a submission may claim is a security rule,
        // and one copy of it is easier to keep right than two.
        await SubmissionMediaLinker.LinkAsync(
            context, fileStorage, definition, answers, template.Id, submissionId, survey.Id, survey.SurveyCode, cancellationToken);

        survey.RecordFill(FilledByRoles.FieldTeam, record.CapturedBy, capturedAt, assignmentId, $"Imported from {sourceCode}.");
        survey.MergeSummary(
            FilledByRoles.FieldTeam,
            SurveyFillSummary.Build(definition, answers, submissionId, FilledByRoles.FieldTeam, record.CapturedBy, capturedAt));

        // Only when the source considered the record closed. The rest stay awaiting review, which is
        // what the source says about them and what a reviewer would want to see.
        if (record.IsApproved)
        {
            survey.Complete(run.RequestedBy, capturedAt, $"Imported from {sourceCode} as '{record.SourceStatus}'.");
        }

        run.AddRecord(DataMigrationRecord.Imported(
            record.RowNumber,
            record.ExternalId,
            survey.Id,
            survey.SurveyCode,
            submissionId,
            media.Imported,
            media.MissingCount,
            media.MissingMessage()));


        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// The survey an imported record becomes, ready to be added but not yet written. A few answers
    /// are lifted onto the survey's own columns — the meter and census numbers — because the
    /// worklist filters on those and would otherwise have to reach into the submission row.
    /// </summary>
    private static Survey BuildSurvey(
        MigrationRecord record,
        string surveyCode,
        SurveyTemplate template,
        long? versionId,
        MigrationRunOptions runOptions,
        string sourceCode,
        IReadOnlyDictionary<string, object?> answers,
        DateTimeOffset capturedAt,
        string? requestedBy)
    {
        var survey = Survey.Create(
            surveyCode,
            template.Id,
            versionId,
            template.CurrentVersionNo,
            SurveySources.Import,
            requestedBy,
            capturedAt,
            faId: null,
            taskCode: null,
            faTypeCode: null,
            runOptions.CbuCode,
            runOptions.BranchCode,
            runOptions.OperationAreaCode,
            runOptions.DepartmentId,
            dueDate: null,
            BuildAdditionalData(record, sourceCode),
            record.Latitude,
            record.Longitude,
            taskTypeId: null,
            customerName: null,
            customerTypeId: null,
            AnswerText(answers, MeterNumberDataName),
            AnswerText(answers, HcnDataName));

        survey.ApplySlaDefaults(template.TeamFillSlaHours, template.CompletionSlaHours);

        // Both clocks: when the crew's device recorded the work, and when we took delivery of it.
        survey.StampFieldCapture(capturedAt, DateTimeOffset.UtcNow);

        // The audit interceptor only fills these in when they are empty, so naming the operator here
        // keeps the run's own author on the row rather than the background job's empty identity.
        survey.CreatedBy = requestedBy;
        survey.LastModifiedBy = requestedBy;

        return survey;
    }

    /// <summary>
    /// Everything that makes a record unusable, as a message — or null when it can be imported.
    /// Checked up front so the failure names the row rather than surfacing as a domain exception
    /// several calls deeper.
    /// </summary>
    private static string? Reject(MigrationRecord record)
    {
        if (record.ParseError is not null)
        {
            return record.ParseError;
        }

        if (string.IsNullOrWhiteSpace(record.ExternalId))
        {
            return "The record carries no external id, so it cannot be identified or de-duplicated.";
        }

        // Survey.Create refuses a placeless survey, and rightly: the map and the worklist both key
        // off the point. Saying so here names the row instead of the aggregate.
        if (record.Latitude is null || record.Longitude is null)
        {
            return "The record has no latitude/longitude, and a survey cannot be placed without one.";
        }

        return null;
    }

    /// <summary>
    /// The survey ids already written for these records, keyed by survey code. One read for the
    /// whole file rather than one per record, and it is what makes a re-run cheap as well as safe.
    /// </summary>
    private async Task<Dictionary<string, long>> LoadExistingCodesAsync(
        string sourceCode,
        IReadOnlyList<MigrationRecord> records,
        CancellationToken cancellationToken)
    {
        var codes = records
            .Where(record => !string.IsNullOrWhiteSpace(record.ExternalId))
            .Select(record => MigrationSurveyCode.For(sourceCode, record.ExternalId))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (codes.Count == 0)
        {
            return new Dictionary<string, long>(StringComparer.Ordinal);
        }

        return await context.Surveys
            .AsNoTracking()
            .Where(survey => codes.Contains(survey.SurveyCode))
            .ToDictionaryAsync(survey => survey.SurveyCode, survey => survey.Id, StringComparer.Ordinal, cancellationToken);
    }

    private async Task<long?> ResolveVersionIdAsync(SurveyTemplate template, CancellationToken cancellationToken) =>
        await context.TemplateVersions
            .AsNoTracking()
            .Where(version => version.TemplateId == template.Id
                && version.VersionNo == template.CurrentVersionNo
                && version.TargetClient == TargetClients.Formly)
            .Select(version => (long?)version.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Everything the source carried that our template does not model, kept on the survey. Nothing
    /// is dropped just because there is no field for it — the export is the only copy of it we will
    /// ever have.
    /// </summary>
    private static string BuildAdditionalData(MigrationRecord record, string sourceCode)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["migrationSource"] = sourceCode,
            ["externalId"] = record.ExternalId,
            ["sourceStatus"] = record.SourceStatus,
            ["sourceCreatedAt"] = record.CapturedAt,
            ["sourceCreatedBy"] = record.CapturedBy,
        };

        foreach (var (key, value) in record.Extras)
        {
            payload[key] = value;
        }

        return JsonSerializer.Serialize(payload, ExtrasSerializerOptions);
    }

    private static string? AnswerText(IReadOnlyDictionary<string, object?> answers, string dataName) =>
        answers.TryGetValue(dataName, out var value) && value is not null
            ? value.ToString()
            : null;

    private async Task FailAsync(DataMigrationRun run, string message)
    {
        if (MigrationRunStatuses.IsTerminal(run.Status))
        {
            return;
        }

        run.Fail(timeProvider.GetUtcNow(), message);

        // Its own token: the run has to be marked failed even when what failed it was the processor
        // cancelling the original one.
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
