using System.Globalization;
using System.Text.Json;
using KH.Application.Common.Interfaces;
using KH.Application.Common.Options;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Definition;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Submissions.Common;
using KH.Application.Fsms.Submissions.Interfaces;
using KH.Application.Fsms.Surveys.Common;
using KH.Application.Fsms.Templates.Common;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Surveys;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Fsms.Surveys.Commands.CreateFieldTeamSurvey;

/// <summary>
/// Records a whole piece of field-raised work in one call: the crew names the scope it is working
/// in, the survey code it generated on the device, the template and the answers, and the API creates
/// the survey, self-allocates it to the caller's own team and writes the submission in one
/// transaction.
///
/// This is the crew raising its own work — filling work the back office allocated to it still goes
/// through <c>POST surveys/{id}/submit</c>. The generated <see cref="SurveyCode"/> is the natural key
/// that makes the call safe to re-send from an offline queue: a code already on record is rejected
/// 409, never replayed.
/// </summary>
[Authorize(Policy = FsmsPolicies.CreateOrManageAssignments)]
public record CreateFieldTeamSurveyCommand : IRequest<Result<FieldTeamSurveySubmissionDto>>
{
    /// <summary>Generated on the device. The natural key of the whole call: a code already on
    /// record is rejected, which is what makes an offline queue safe to re-send.</summary>
    public string SurveyCode { get; init; } = default!;

    public long TemplateId { get; init; }

    /// <summary>The ORG_SCOPES row the crew chose to work in, from the login response.</summary>
    public long ScopeId { get; init; }

    /// <summary>When the device recorded the survey. See Survey.StampFieldCapture.</summary>
    public DateTimeOffset DeviceCreatedAt { get; init; }

    /// <summary>Answers keyed by field <c>data_name</c>. Unknown keys are ignored by the store.</summary>
    public Dictionary<string, object?> Answers { get; init; } = new();

    public Guid? ClientSubmissionId { get; init; }
    public string? FaId { get; init; }
    public string? TaskCode { get; init; }
    public string? FaTypeCode { get; init; }
    public long? TaskTypeId { get; init; }
    public string? CustomerName { get; init; }

    /// <summary>The number the crew calls before arriving.</summary>
    public string? CustomerPhone { get; init; }

    public long? CustomerTypeId { get; init; }
    public string? MeterNumber { get; init; }
    public string? Hcn { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public Dictionary<string, object?>? AdditionalData { get; init; }
}

/// <summary>
/// The record of a field-raised survey and its first fill. Not <c>SurveyDetailDto</c>: that echoes
/// the full definition back to a client that already has it, and carries no submission id.
/// </summary>
public sealed class FieldTeamSurveySubmissionDto
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;
    public string Status { get; init; } = default!;
    public long TemplateId { get; init; }
    public int? TemplateVersionNo { get; init; }
    public long AssignmentId { get; init; }
    public long SubmissionId { get; init; }
    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public int? DepartmentId { get; init; }
    public DateTimeOffset DeviceCreatedDate { get; init; }
    public DateTimeOffset ReceivedDate { get; init; }
}

public sealed class CreateFieldTeamSurveyCommandValidator : AbstractValidator<CreateFieldTeamSurveyCommand>
{
    /// <summary>
    /// A device clock this far ahead of the server, or this far in the past, is not drift — it is a
    /// wrongly set clock, and stamping the survey with it would misdescribe when the work was done.
    /// </summary>
    private static readonly DateTimeOffset EarliestSaneCapture = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public CreateFieldTeamSurveyCommandValidator()
    {
        RuleFor(x => x.SurveyCode)
            .NotEmpty().WithMessage("Survey code is required.")
            .MaximumLength(60).WithMessage("Survey code must not exceed 60 characters.");

        RuleFor(x => x.TemplateId)
            .GreaterThan(0).WithMessage("Template id must be greater than 0.");

        RuleFor(x => x.ScopeId)
            .GreaterThan(0).WithMessage("Scope id must be greater than 0.");

        RuleFor(x => x.DeviceCreatedAt)
            .NotEqual(default(DateTimeOffset)).WithMessage("The device capture time is required.")
            .GreaterThanOrEqualTo(EarliestSaneCapture)
                .WithMessage("The device capture time is implausibly far in the past — check the device clock.")
            .Must(BeWithinReach)
                .WithMessage("The device capture time is more than a day ahead — check the device clock.");

        RuleFor(x => x.Answers)
            .NotNull().WithMessage("Answers are required.")
            .Must(a => a is { Count: > 0 }).WithMessage("At least one answer is required.");

        RuleFor(x => x.ClientSubmissionId)
            .NotEqual(Guid.Empty).WithMessage("Client submission id must not be empty.")
            .When(x => x.ClientSubmissionId.HasValue);

        RuleFor(x => x.FaId)
            .MaximumLength(100).WithMessage("FA id must not exceed 100 characters.");

        RuleFor(x => x.TaskCode)
            .MaximumLength(100).WithMessage("Task code must not exceed 100 characters.");

        RuleFor(x => x.FaTypeCode)
            .MaximumLength(50).WithMessage("FA type code must not exceed 50 characters.");

        RuleFor(x => x.TaskTypeId)
            .GreaterThan(0L).WithMessage("Task type id must be a positive number.")
            .When(x => x.TaskTypeId.HasValue);

        RuleFor(x => x.CustomerName)
            .MaximumLength(SurveyFieldLimits.CustomerName)
            .WithMessage($"Customer name must not exceed {SurveyFieldLimits.CustomerName} characters.");

        RuleFor(x => x.CustomerPhone)
            .MaximumLength(SurveyFieldLimits.CustomerPhone)
            .WithMessage($"Customer phone must not exceed {SurveyFieldLimits.CustomerPhone} characters.");

        RuleFor(x => x.CustomerTypeId)
            .GreaterThan(0L).WithMessage("Customer type id must be a positive number.")
            .When(x => x.CustomerTypeId.HasValue);

        RuleFor(x => x.MeterNumber)
            .MaximumLength(SurveyFieldLimits.MeterNumber)
            .WithMessage($"Meter number must not exceed {SurveyFieldLimits.MeterNumber} characters.");

        RuleFor(x => x.Hcn)
            .MaximumLength(SurveyFieldLimits.Hcn)
            .WithMessage($"HCN must not exceed {SurveyFieldLimits.Hcn} characters.");

        RuleFor(x => x.Latitude)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Latitude is required.")
            .InclusiveBetween(-90, 90).WithMessage("Latitude must be between -90 and 90.");

        RuleFor(x => x.Longitude)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Longitude is required.")
            .InclusiveBetween(-180, 180).WithMessage("Longitude must be between -180 and 180.");
    }

    private static bool BeWithinReach(DateTimeOffset deviceCreatedAt) =>
        deviceCreatedAt <= DateTimeOffset.UtcNow.AddHours(24);
}

public sealed class CreateFieldTeamSurveyCommandHandler(
    IApplicationDbContext context,
    ISurveySubmissionStore submissionStore,
    IOrgScopeService orgScopeService,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings,
    IUser user,
    TimeProvider timeProvider,
    IFileStorage fileStorage,
    IOptions<FileStorageOptions> fileStorageOptions,
    IOptions<SurveyTimeOptions> surveyTimeOptions,
    IOptions<SurveyValidationOptions> surveyValidationOptions)
    : IRequestHandler<CreateFieldTeamSurveyCommand, Result<FieldTeamSurveySubmissionDto>>
{
    private const string OnlyFieldTeamMessage = "Only a field team can raise a survey from the field.";
    private const string SelfAllocationNote = "Self-allocated by the field crew that raised it.";
    private const string SurveyCodeIndexName = "IX_SURVEYS_SurveyCode";

    /// <summary>The territory a working scope expands to, whatever level the scope sits at.</summary>
    private sealed record ResolvedScope(string? CbuCode, string? BranchCode, string? OperationAreaCode, int? DepartmentId);

    public async Task<Result<FieldTeamSurveySubmissionDto>> Handle(
        CreateFieldTeamSurveyCommand request, CancellationToken cancellationToken)
    {
        // Only a crew raises work from the field — a back-office login has no team to allocate it to.
        if (user.FieldTeamId is not long fieldTeamId)
        {
            return Result<FieldTeamSurveySubmissionDto>.Fail(
                OnlyFieldTeamMessage, ApiErrorCodes.UnauthorizedAccess, httpStatusCode: 403);
        }

        var scopeResult = await ResolveScopeAsync(request.ScopeId, fieldTeamId, cancellationToken);
        if (!scopeResult.IsSuccess || scopeResult.Data is null)
        {
            return Result<FieldTeamSurveySubmissionDto>.Fail(scopeResult.Errors);
        }

        var scope = scopeResult.Data;

        var resolution = await SurveyTemplateResolver.ResolveAsync(
            context, request.TemplateId, templateCode: null, faTypeCode: null, cancellationToken);

        if (!resolution.IsSuccess || resolution.Data is null)
        {
            return Result<FieldTeamSurveySubmissionDto>.Fail(resolution.Errors);
        }

        var resolved = resolution.Data;

        // The same territory guard the back-office create path uses: a crew may only raise a survey
        // against a template its own coverage reaches.
        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);
        if (!await TemplateScopeGuard.CanSeeAsync(orgScopeService, callerScope, resolved.TemplateId, cancellationToken))
        {
            return Result<FieldTeamSurveySubmissionDto>.Fail(
                TemplateScopeGuard.OutsideTerritoryMessage, ApiErrorCodes.UnauthorizedAccess, httpStatusCode: 403);
        }

        var surveyCode = request.SurveyCode.Trim();

        // A code already on record means the crew's queue is re-sending work already accepted. Always
        // 409, never a replay: the fill has moved on since, so answering 200 would misdescribe it.
        if (await context.Surveys.AnyAsync(x => x.SurveyCode == surveyCode, cancellationToken))
        {
            return Conflict(surveyCode);
        }

        var template = await context.SurveyTemplates
            .FirstOrDefaultAsync(x => x.Id == resolved.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result<FieldTeamSurveySubmissionDto>.Fail(
                "Template not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        if (template.Status != TemplateStatuses.Published)
        {
            return Result<FieldTeamSurveySubmissionDto>.Fail(
                "Only a published template can accept submissions.", ApiErrorCodes.ValidationError, httpStatusCode: 400);
        }

        if (!SurveyDefinitionParser.HasElements(template.DefinitionJson))
        {
            return Result<FieldTeamSurveySubmissionDto>.Fail(
                "The template has no fields to submit against.", ApiErrorCodes.ValidationError, httpStatusCode: 400);
        }

        var definition = SurveyDefinitionParser.Parse(template.DefinitionJson);

        // Re-keyed onto the trimmed names the definition is read under, before anything matches
        // answers against it — see SurveyAnswerKeys.
        var answers = SurveyAnswerKeys.Normalize(request.Answers);

        // Checked here, before the survey is created rather than after: this call raises the survey
        // and records its first fill together, so a refused fill must leave nothing behind. The
        // device's own capture time is the clock the date rules are judged against — this is the
        // offline path, and the form may have been filled well before it reached us.
        var answerErrors = SurveyAnswerValidator.Validate(
            definition,
            answers,
            ResolveFillClock(request.DeviceCreatedAt),
            surveyValidationOptions.Value.EnforceFieldRules);

        if (answerErrors.Count > 0)
        {
            return Result<FieldTeamSurveySubmissionDto>.Fail(answerErrors
                .Select(error => new ErrorInfo(error.Message, ApiErrorCodes.ValidationError, httpStatusCode: 400))
                .ToList());
        }

        // Schema reconciliation is DDL and must run before the transaction opens — adding a column
        // inside the transaction would hold a schema lock for its duration.
        await submissionStore.ReconcileTableAsync(template, cancellationToken);

        // The store writes through native SQL, outside EF's change tracker, so its insert would
        // otherwise be committed before the survey it belongs to. One transaction over both keeps a
        // failed call from leaving a submission row whose survey never advanced.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var receivedAt = timeProvider.GetUtcNow();
        var versionId = await SurveyTemplateResolver.ResolveVersionIdAsync(context, resolved, cancellationToken);

        var survey = Survey.Create(
            surveyCode,
            resolved.TemplateId,
            versionId,
            resolved.CurrentVersionNo,
            SurveySources.Team,
            user.Id,
            receivedAt,
            request.FaId,
            request.TaskCode,
            request.FaTypeCode,
            scope.CbuCode,
            scope.BranchCode,
            scope.OperationAreaCode,
            scope.DepartmentId,
            dueDate: null,
            request.AdditionalData is { Count: > 0 } ? JsonSerializer.Serialize(request.AdditionalData) : null,
            request.Latitude,
            request.Longitude,
            request.TaskTypeId,
            request.CustomerName,
            request.CustomerTypeId,
            request.MeterNumber,
            request.Hcn,
            request.CustomerPhone);

        survey.StampFieldCapture(request.DeviceCreatedAt, receivedAt);
        survey.ApplySlaDefaults(resolved.TeamFillSlaHours, resolved.CompletionSlaHours);
        var assignment = survey.Assign(fieldTeamId, user.Id, receivedAt, dueDate: null, note: SelfAllocationNote);

        context.Surveys.Add(survey);

        // Check-then-insert on a client-generated code collides far more often than a server code
        // when the offline queue re-sends. Answer the unique-index violation with the same 409 as the
        // pre-check, so a retry sees one consistent answer either way rather than a raw 500.
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsSurveyCodeConflict(exception))
        {
            return Conflict(surveyCode);
        }

        await SignatureDataUrlNormalizer.NormalizeAsync(
            context,
            fileStorage,
            fileStorageOptions.Value,
            definition,
            template.Id,
            survey.Id,
            answers,
            cancellationToken);

        var submissionId = await submissionStore.InsertAsync(
            new SubmissionInsert
            {
                TemplateId = template.Id,
                VersionNo = survey.TemplateVersionNo ?? template.CurrentVersionNo,
                SubmittedBy = user.Id,
                SurveyId = survey.Id,
                AssignmentId = assignment.Id,
                FilledByRole = FilledByRoles.FieldTeam,
                ClientSubmissionId = request.ClientSubmissionId,
                Answers = answers,
            },
            cancellationToken);

        await SubmissionMediaLinker.LinkAsync(
            context, fileStorage, definition, answers, template.Id, submissionId, survey.Id, survey.SurveyCode, cancellationToken);

        survey.RecordFill(FilledByRoles.FieldTeam, user.Id, receivedAt, assignment.Id);
        survey.MergeSummary(
            FilledByRoles.FieldTeam,
            SurveyFillSummary.Build(definition, answers, submissionId, FilledByRoles.FieldTeam, user.Id, receivedAt));

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<FieldTeamSurveySubmissionDto>.Success(new FieldTeamSurveySubmissionDto
        {
            SurveyId = survey.Id,
            SurveyCode = survey.SurveyCode,
            Status = survey.Status,
            TemplateId = survey.TemplateId,
            TemplateVersionNo = survey.TemplateVersionNo,
            AssignmentId = assignment.Id,
            SubmissionId = submissionId,
            CbuCode = scope.CbuCode,
            BranchCode = scope.BranchCode,
            OperationAreaCode = scope.OperationAreaCode,
            DepartmentId = scope.DepartmentId,
            DeviceCreatedDate = request.DeviceCreatedAt,
            ReceivedDate = receivedAt,
        });
    }

    /// <summary>
    /// Turns the working scope the crew chose into the territory to stamp onto the survey. The level
    /// is irrelevant to the caller: an operation-area scope fills all three codes, a branch scope fills
    /// branch plus its parent CBU, a CBU scope fills the CBU alone — and the survey is reachable by
    /// <c>OrgScopeSet.Covers</c> at every level in between.
    /// </summary>
    private async Task<Result<ResolvedScope>> ResolveScopeAsync(
        long scopeId, long fieldTeamId, CancellationToken cancellationToken)
    {
        var scope = await context.OrgScopes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == scopeId && x.IsActive, cancellationToken);

        if (scope is null)
        {
            return Result<ResolvedScope>.Fail(
                "Working scope not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        // The check that stops a crew passing another team's scope id.
        if (scope.OwnerType != OrgScopeOwnerTypes.Team
            || scope.OwnerId != fieldTeamId.ToString(CultureInfo.InvariantCulture))
        {
            return Result<ResolvedScope>.Fail(
                "That working scope does not belong to your field team.", ApiErrorCodes.UnauthorizedAccess, httpStatusCode: 403);
        }

        // A cluster-only scope, or a department-only scope with no territory, matches nothing in a
        // scoped worklist — the survey would be invisible even to the crew that raised it.
        if (!scope.HasTerritory || scope.Level == OrgScopeLevels.Cluster)
        {
            return Result<ResolvedScope>.Fail(
                "Select a CBU, branch or operation area scope — a cluster scope is too broad to raise work in.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        var hierarchy = await OrgHierarchyCache.GetAsync(context, cache, cacheSettings.Value, cancellationToken);
        var entry = hierarchy.Find(scope.Level!, scope.Code!);

        if (entry is null)
        {
            return Result<ResolvedScope>.Fail(
                "The org unit behind this scope no longer exists.", ApiErrorCodes.ValidationError, httpStatusCode: 400);
        }

        return Result<ResolvedScope>.Success(
            new ResolvedScope(entry.CbuCode, entry.BranchCode, entry.OperationAreaCode, scope.DepartmentId));
    }

    private static Result<FieldTeamSurveySubmissionDto> Conflict(string surveyCode) =>
        Result<FieldTeamSurveySubmissionDto>.Fail(
            $"Survey '{surveyCode}' has already been submitted.", ApiErrorCodes.ValidationError, httpStatusCode: 409);

    /// <summary>
    /// The local wall clock this fill's date rules are judged against. See the same method on
    /// <c>SubmitSurveyCommandHandler</c> — the device's capture time is honoured so a form filled
    /// out of coverage is judged on the day the work was done, but never later than the server's own
    /// clock, so a device with a fast clock cannot walk past a "must not be in the future" rule.
    /// </summary>
    private DateTime ResolveFillClock(DateTimeOffset deviceCreatedAt)
    {
        var branch = surveyTimeOptions.Value.ResolveTimeZone();
        var serverNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), branch).DateTime;
        var deviceNow = TimeZoneInfo.ConvertTime(deviceCreatedAt, branch).DateTime;

        return deviceNow < serverNow ? deviceNow : serverNow;
    }

    /// <summary>
    /// Whether a save failure was the survey-code unique index rejecting a duplicate. Walks the
    /// exception chain for the index name rather than depending on a provider-specific error number.
    /// </summary>
    private static bool IsSurveyCodeConflict(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(SurveyCodeIndexName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
