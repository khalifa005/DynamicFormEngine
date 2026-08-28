using KH.Application.Common.Interfaces;
using KH.Application.Common.Options;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Templates.Common;
using KH.Application.Fsms.DataMigration.Commands.StartDataMigration;
using KH.Application.Fsms.DataMigration.Common;
using KH.Application.Fsms.DataMigration.Jobs;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Migration;
using Microsoft.Extensions.Options;
using Shared.Core.Common;

namespace KH.Application.Fsms.DataMigration.Commands.RetryMigrationMedia;

/// <summary>
/// Attaches media that reached the archive after an import ran.
///
/// The case this answers: an import placed 948 surveys while some of their photos were still being
/// copied to the server. Those records imported cleanly and are short a file each. Re-running the
/// same workbook cannot fix them — the import skips a record whose survey exists, which is the guard
/// that makes an interrupted import safe to resume and is not worth weakening for this.
///
/// So it re-runs the same file in <see cref="MigrationModes.BackfillMedia"/>. Nothing is uploaded:
/// the workbook is already on the server at the original run's <c>StoredPath</c>, and its placement
/// choices are already in its <c>OptionsJson</c>. Re-uploading a large file to say "the same again"
/// would be waste, and a chance to pick the wrong file.
/// </summary>
[Authorize(Policy = FsmsPolicies.ImportData)]
public record RetryMigrationMediaCommand : IRequest<Result<StartedMigrationRunDto>>
{
    /// <summary>The completed import whose file and options the retry reuses.</summary>
    public long RunId { get; init; }
}

public sealed class RetryMigrationMediaCommandValidator : AbstractValidator<RetryMigrationMediaCommand>
{
    public RetryMigrationMediaCommandValidator()
    {
        RuleFor(x => x.RunId)
            .GreaterThan(0).WithMessage("Run id is required.");
    }
}

public sealed class RetryMigrationMediaCommandHandler(
    IApplicationDbContext context,
    IMigrationSourceRegistry sourceRegistry,
    IFileStorage fileStorage,
    IOrgScopeService orgScopeService,
    IBackgroundJobScheduler jobScheduler,
    IUser user,
    TimeProvider timeProvider,
    IOptions<DataMigrationOptions> options)
    : IRequestHandler<RetryMigrationMediaCommand, Result<StartedMigrationRunDto>>
{
    public async Task<Result<StartedMigrationRunDto>> Handle(
        RetryMigrationMediaCommand request,
        CancellationToken cancellationToken)
    {
        var source = await context.DataMigrationRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.RunId, cancellationToken);

        if (source is null)
        {
            return Fail("Migration run not found.", ApiErrorCodes.NotFound, 404);
        }

        // Only an import leaves surveys to attach media to. Retrying a validate run would find no
        // survey for any record and report 948 rows of "not imported yet", which is a confusing way
        // of saying the operator picked the wrong run.
        if (source.Mode != MigrationModes.Import)
        {
            return Fail(
                "Media can only be retried for an import run — a validate run wrote no surveys to attach files to.",
                ApiErrorCodes.ValidationError,
                400);
        }

        if (!MigrationRunStatuses.IsTerminal(source.Status))
        {
            return Fail("This run is still in progress. Wait for it to finish before retrying its media.", ApiErrorCodes.ValidationError, 400);
        }

        if (sourceRegistry.Find(source.SourceCode) is null)
        {
            return Fail($"No importer is registered for source '{source.SourceCode}'.", ApiErrorCodes.ValidationError, 400);
        }

        // Same pre-flight as the import: an archive that is not there would report every file missing
        // several hundred rows later rather than saying so now.
        var archive = fileStorage.Describe(options.Value.ArchiveFolder);

        if (!archive.Exists)
        {
            return Fail(
                $"The migration archive folder does not exist. Create '{archive.FullPath}' and copy the exported media into it before retrying.",
                ApiErrorCodes.ValidationError,
                400);
        }

        // The retry writes to the surveys the original run placed, so the caller has to reach the
        // same ground. Re-derived from the stored options rather than trusted from the original run:
        // the operator retrying may not be the one who imported.
        var runOptions = MigrationRunOptions.FromJson(source.OptionsJson);
        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);

        if (!callerScope.Covers(runOptions.CbuCode, runOptions.BranchCode, runOptions.OperationAreaCode, runOptions.DepartmentId))
        {
            return Fail("You cannot change imported surveys outside your territory.", ApiErrorCodes.UnauthorizedAccess, 403);
        }

        if (!await TemplateScopeGuard.CanSeeAsync(orgScopeService, callerScope, source.TemplateId, cancellationToken))
        {
            return Fail(TemplateScopeGuard.OutsideTerritoryMessage, ApiErrorCodes.UnauthorizedAccess, 403);
        }

        // The stored workbook is not checked here: the job opens it and already reports a missing one
        // as a failed run with its own message, and duplicating that would mean a new storage method
        // for a check that is made anyway a second later.
        //
        // Both runs point at one stored file. Nothing deletes uploaded workbooks, so sharing is safe
        // — and it is the point: the retry must read exactly the bytes the import read, not a file
        // someone believes to be the same.
        var run = DataMigrationRun.Create(
            source.SourceCode,
            MigrationModes.BackfillMedia,
            source.TemplateId,
            source.FileName,
            source.StoredPath,
            source.OptionsJson,
            user.Id);

        context.DataMigrationRuns.Add(run);
        await context.SaveChangesAsync(cancellationToken);

        if (!jobScheduler.Enqueue<IDataMigrationJob>(job => job.RunAsync(run.Id, CancellationToken.None)))
        {
            run.Fail(timeProvider.GetUtcNow(), "No background job processor is available, so the media retry was not started.");
            await context.SaveChangesAsync(cancellationToken);

            return Fail(
                "The media retry could not be queued because no background job processor is available.",
                ApiErrorCodes.ValidationError,
                503);
        }

        return Result<StartedMigrationRunDto>.Success(
            new StartedMigrationRunDto(run.Id, run.Status, run.Mode, run.SourceCode));
    }

    private static Result<StartedMigrationRunDto> Fail(string message, string code, int httpStatusCode) =>
        Result<StartedMigrationRunDto>.Fail(message, code, httpStatusCode: httpStatusCode);
}
