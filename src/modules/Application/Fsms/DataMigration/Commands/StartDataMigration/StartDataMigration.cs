using KH.Application.Common.Interfaces;
using KH.Application.Common.Options;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.DataMigration.Common;
using KH.Application.Fsms.DataMigration.Jobs;
using KH.Application.Fsms.Templates.Common;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Migration;
using Microsoft.Extensions.Options;
using Shared.Core.Common;

namespace KH.Application.Fsms.DataMigration.Commands.StartDataMigration;

/// <summary>
/// Queues an import of an external data set. The request only takes delivery of the file and
/// records what the operator asked for — the work itself runs out of band, because a file of several
/// hundred records copying several thousand photos is minutes of work and no HTTP request should be
/// held open for it.
/// </summary>
[Authorize(Policy = FsmsPolicies.ImportData)]
public record StartDataMigrationCommand : IRequest<Result<StartedMigrationRunDto>>
{
    /// <summary>Which external system the file came from — see <see cref="MigrationSourceCodes"/>.</summary>
    public string SourceCode { get; init; } = string.Empty;

    /// <summary>See <see cref="MigrationModes"/>. Defaults to the harmless one.</summary>
    public string Mode { get; init; } = MigrationModes.Validate;

    /// <summary>
    /// The published form the file's columns are to land on. Chosen by the operator, because one
    /// source exports many different apps and only they know which of ours this export answers to.
    /// </summary>
    public long TemplateId { get; init; }

    public string FileName { get; init; } = string.Empty;

    /// <summary>Size reported by the request; the stored size is re-measured after writing.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Request body stream — read once by the handler, never buffered into memory.</summary>
    public Stream Content { get; init; } = Stream.Null;

    // Where every survey in this run is placed. Not optional: the survey list matches on CBU, branch
    // or area, so a placeless import would be invisible to the person who ran it.
    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public int? DepartmentId { get; init; }

    /// <summary>Optional crew to attribute the imported work to.</summary>
    public long? FieldTeamId { get; init; }
}

/// <summary>The queued run, so the page can start polling it straight away.</summary>
public sealed record StartedMigrationRunDto(long RunId, string Status, string Mode, string SourceCode);

public sealed class StartDataMigrationCommandValidator : AbstractValidator<StartDataMigrationCommand>
{
    public StartDataMigrationCommandValidator()
    {
        RuleFor(x => x.SourceCode)
            .NotEmpty().WithMessage("A migration source is required.")
            .Must(MigrationSourceCodes.IsDefined).WithMessage("The migration source is not recognized.");

        RuleFor(x => x.Mode)
            .NotEmpty().WithMessage("A migration mode is required.")
            .Must(MigrationModes.IsDefined).WithMessage("The migration mode is not recognized.");

        RuleFor(x => x.TemplateId)
            .GreaterThan(0L).WithMessage("A target template is required.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("A file name is required.")
            .MaximumLength(400).WithMessage("The file name must not exceed 400 characters.");

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage("An empty file cannot be imported.");

        RuleFor(x => x.CbuCode)
            .MaximumLength(50).WithMessage("CBU code must not exceed 50 characters.");

        RuleFor(x => x.BranchCode)
            .MaximumLength(50).WithMessage("Branch code must not exceed 50 characters.");

        RuleFor(x => x.OperationAreaCode)
            .MaximumLength(50).WithMessage("Operation area code must not exceed 50 characters.");

        RuleFor(x => x.FieldTeamId)
            .GreaterThan(0L).WithMessage("Field team id must be a positive number.")
            .When(x => x.FieldTeamId.HasValue);

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.CbuCode)
                || !string.IsNullOrWhiteSpace(x.BranchCode)
                || !string.IsNullOrWhiteSpace(x.OperationAreaCode)
                || x.DepartmentId is > 0)
            .WithMessage("Imported surveys must be placed: name at least a CBU, branch, operation area or department.");
    }
}

public sealed class StartDataMigrationCommandHandler(
    IApplicationDbContext context,
    IMigrationSourceRegistry sourceRegistry,
    IFileStorage fileStorage,
    IOrgScopeService orgScopeService,
    IBackgroundJobScheduler jobScheduler,
    IUser user,
    TimeProvider timeProvider,
    IOptions<DataMigrationOptions> options)
    : IRequestHandler<StartDataMigrationCommand, Result<StartedMigrationRunDto>>
{
    private const int BytesPerMb = 1024 * 1024;

    /// <summary>Subfolder under the storage root where uploaded source files are parked.</summary>
    private const string UploadFolder = "migration";

    public async Task<Result<StartedMigrationRunDto>> Handle(
        StartDataMigrationCommand request,
        CancellationToken cancellationToken)
    {
        var adapter = sourceRegistry.Find(request.SourceCode);
        if (adapter is null)
        {
            return Fail($"No importer is registered for source '{request.SourceCode}'.", ApiErrorCodes.ValidationError, 400);
        }

        var settings = options.Value;

        var maxBytes = (long)settings.MaxUploadMb * BytesPerMb;
        if (request.SizeBytes > maxBytes)
        {
            return Fail($"The file exceeds the {settings.MaxUploadMb} MB import limit.", ApiErrorCodes.ValidationError, 400);
        }

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!adapter.AcceptedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return Fail(
                $"'{request.FileName}' cannot be read by the {adapter.SourceCode} importer. Accepted: {string.Join(", ", adapter.AcceptedExtensions)}.",
                ApiErrorCodes.ValidationError,
                400);
        }

        // Checked before anything is written rather than when the first photo is looked for: an
        // import that quietly produced hundreds of survey rows with no media at all would read as a
        // success and be far more work to undo than to refuse here.
        var archive = fileStorage.Describe(settings.ArchiveFolder);

        if (!archive.Exists)
        {
            return Fail(
                $"The migration archive folder does not exist. Create '{archive.FullPath}' and copy the exported media into it before importing.",
                ApiErrorCodes.ValidationError,
                400);
        }

        // Containment, exactly as CreateSurvey applies it: this run places every one of its surveys
        // somewhere, so the caller's coverage has to reach that spot.
        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);

        if (!callerScope.Covers(request.CbuCode, request.BranchCode, request.OperationAreaCode, request.DepartmentId))
        {
            return Fail("You cannot import surveys outside your territory.", ApiErrorCodes.UnauthorizedAccess, 403);
        }

        var template = await context.SurveyTemplates
            .AsNoTracking()
            .Where(x => x.Id == request.TemplateId)
            .Select(x => new { x.Id, x.Status, x.TemplateCode })
            .FirstOrDefaultAsync(cancellationToken);

        if (template is null)
        {
            return Fail("Template not found.", ApiErrorCodes.NotFound, 404);
        }

        // The operator names the template now, so the same territory rule that governs reading one
        // has to govern importing into it — otherwise anyone holding the permission could pour
        // several hundred surveys into another operation area's form.
        if (!await TemplateScopeGuard.CanSeeAsync(orgScopeService, callerScope, template.Id, cancellationToken))
        {
            return Fail(TemplateScopeGuard.OutsideTerritoryMessage, ApiErrorCodes.UnauthorizedAccess, 403);
        }

        if (template.Status != TemplateStatuses.Published)
        {
            return Fail($"Template '{template.TemplateCode}' is not published, so it cannot accept imported records.", ApiErrorCodes.ValidationError, 400);
        }

        if (request.FieldTeamId is long fieldTeamId
            && !await context.FieldTeams.AnyAsync(x => x.Id == fieldTeamId, cancellationToken))
        {
            return Fail("Field team not found.", ApiErrorCodes.NotFound, 404);
        }

        // The job runs long after this request has ended, so the bytes have to outlive it.
        var storedName = $"{timeProvider.GetUtcNow().UtcDateTime:yyyyMMddHHmmss}-{Guid.NewGuid():N}{extension}";

        var stored = await fileStorage.SaveAsync(
            request.Content,
            storedName,
            MigrationContentTypes.For(extension),
            UploadFolder,
            cancellationToken);

        if (stored.SizeBytes > maxBytes)
        {
            // The declared size lied; drop what was written rather than keep an over-limit file.
            await fileStorage.DeleteAsync(stored.RelativePath, cancellationToken);
            return Fail($"The file exceeds the {settings.MaxUploadMb} MB import limit.", ApiErrorCodes.ValidationError, 400);
        }

        var runOptions = new MigrationRunOptions
        {
            CbuCode = request.CbuCode,
            BranchCode = request.BranchCode,
            OperationAreaCode = request.OperationAreaCode,
            DepartmentId = request.DepartmentId,
            FieldTeamId = request.FieldTeamId,
        };

        var run = DataMigrationRun.Create(
            adapter.SourceCode,
            request.Mode,
            template.Id,
            request.FileName,
            stored.RelativePath,
            runOptions.ToJson(),
            user.Id);

        context.DataMigrationRuns.Add(run);
        await context.SaveChangesAsync(cancellationToken);

        // The run row is saved first on purpose: it is what the operator polls, so a processor that
        // is not running leaves a visible PENDING run rather than a request that appeared to work
        // and did nothing.
        if (!jobScheduler.Enqueue<IDataMigrationJob>(job => job.RunAsync(run.Id, CancellationToken.None)))
        {
            run.Fail(timeProvider.GetUtcNow(), "No background job processor is available, so the import was not started.");
            await context.SaveChangesAsync(cancellationToken);

            return Fail(
                "The import could not be queued because no background job processor is available.",
                ApiErrorCodes.ValidationError,
                503);
        }

        return Result<StartedMigrationRunDto>.Success(
            new StartedMigrationRunDto(run.Id, run.Status, run.Mode, run.SourceCode));
    }

    private static Result<StartedMigrationRunDto> Fail(string message, string code, int httpStatusCode) =>
        Result<StartedMigrationRunDto>.Fail(message, code, httpStatusCode: httpStatusCode);
}
