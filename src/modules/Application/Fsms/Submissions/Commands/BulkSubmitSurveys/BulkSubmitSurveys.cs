using KH.Application.Common.Interfaces;
using KH.Application.Common.Options;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Definition;
using KH.Application.Fsms.Submissions.Common;
using KH.Application.Fsms.Submissions.Interfaces;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Surveys;
using KH.Domain.Entities.Fsms.Templates;
using Microsoft.Extensions.Options;
using Shared.Core.Common;
using Shared.Core.Exceptions;

namespace KH.Application.Fsms.Submissions.Commands.BulkSubmitSurveys;

/// <summary>One queued fill from the mobile app's offline store.</summary>
public record BulkSubmitSurveyItem
{
    /// <summary>
    /// The app's own key for this fill, generated once when the crew saved the form on the device.
    /// Required here — a queue that cannot be replayed safely is not worth replaying at all — and
    /// the same value must come back on every retry.
    /// </summary>
    public Guid ClientSubmissionId { get; init; }

    public long SurveyId { get; init; }

    /// <summary>Cross-checks the survey's own template, as on a single submit. Optional.</summary>
    public long? TemplateId { get; init; }

    /// <summary>
    /// When the crew actually filled this form, in their own local time. It matters more here than
    /// anywhere else: this is the queue that drains days after the work was done, and a date rule
    /// judged against the moment the connection came back would refuse a fill that was correct when
    /// it was made. Clamped to the server clock, as on a single submit.
    /// </summary>
    public DateTimeOffset? ClientFilledAt { get; init; }

    /// <summary>Answers keyed by field <c>data_name</c>. Unknown keys are ignored by the store.</summary>
    public Dictionary<string, object?> Answers { get; init; } = new();
}

/// <summary>
/// Drains a mobile app's offline queue in one call: every survey the crew filled while out of
/// coverage, sent when the connection comes back.
///
/// Partial success is the point, as it is for a bulk allocation — a run of thirty fills must not be
/// lost because one survey was re-allocated while the crew was offline. Every item is checked and
/// reported on its own.
///
/// Replays are safe. Each item carries the client's own key, and an item already recorded under that
/// key comes back marked <see cref="BulkSubmissionItemResultDto.Duplicate"/> with the original
/// submission id rather than being written a second time, so an app that never saw the response to
/// its last attempt can simply send the whole queue again.
///
/// Media is uploaded first and referenced here: the answers name file ids the app obtained from the
/// upload endpoint on reconnect. No bytes travel in this payload.
/// </summary>
[Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
public record BulkSubmitSurveysCommand : IRequest<Result<BulkSubmissionResultDto>>
{
    public IReadOnlyList<BulkSubmitSurveyItem> Items { get; init; } = [];
}

public sealed class BulkSubmitSurveysCommandValidator : AbstractValidator<BulkSubmitSurveysCommand>
{
    /// <summary>
    /// Deliberately well below the bulk-allocation cap: every item here writes a submission row,
    /// claims its media and moves an aggregate, and the call travels over whatever connection the
    /// crew has just regained. A longer queue is drained in several calls.
    /// </summary>
    public const int MaxItems = 50;

    public BulkSubmitSurveysCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one submission is required.")
            .Must(items => items.Count <= MaxItems)
            .WithMessage($"No more than {MaxItems} submissions can be sent at once.")
            .Must(items => items.Select(i => i.ClientSubmissionId).Distinct().Count() == items.Count)
            .WithMessage("Each submission must carry its own client submission id.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.ClientSubmissionId)
                .NotEqual(Guid.Empty).WithMessage("Client submission id is required.");

            item.RuleFor(x => x.SurveyId)
                .GreaterThan(0).WithMessage("Survey id is required.");

            item.RuleFor(x => x.TemplateId)
                .GreaterThan(0).WithMessage("Template id must be greater than 0.")
                .When(x => x.TemplateId.HasValue);

            item.RuleFor(x => x.Answers)
                .NotNull().WithMessage("Answers are required.")
                .Must(a => a is { Count: > 0 }).WithMessage("At least one answer is required.");
        });
    }
}

/// <summary>What became of one queued fill.</summary>
public sealed class BulkSubmissionItemResultDto
{
    public Guid ClientSubmissionId { get; init; }
    public long SurveyId { get; init; }
    public string? SurveyCode { get; init; }

    /// <summary>The submission recorded — set on a success and on a duplicate alike.</summary>
    public long? SubmissionId { get; init; }

    public bool Success { get; init; }

    /// <summary>
    /// True when this fill had already been accepted under the same key. The app clears its queue
    /// row on this exactly as it does on a success: the work is recorded either way.
    /// </summary>
    public bool Duplicate { get; init; }

    /// <summary>Why it failed. Null unless <see cref="Success"/> is false.</summary>
    public string? Message { get; init; }
}

public sealed class BulkSubmissionResultDto
{
    public int SucceededCount { get; init; }
    public int DuplicateCount { get; init; }
    public int FailedCount { get; init; }
    public IReadOnlyList<BulkSubmissionItemResultDto> Results { get; init; } = [];
}

public sealed class BulkSubmitSurveysCommandHandler(
    IApplicationDbContext context,
    ISurveySubmissionStore submissionStore,
    IUser user,
    TimeProvider timeProvider,
    IFileStorage fileStorage,
    IOptions<FileStorageOptions> fileStorageOptions,
    IOptions<SurveyTimeOptions> surveyTimeOptions,
    IOptions<SurveyValidationOptions> surveyValidationOptions)
    : IRequestHandler<BulkSubmitSurveysCommand, Result<BulkSubmissionResultDto>>
{
    public async Task<Result<BulkSubmissionResultDto>> Handle(
        BulkSubmitSurveysCommand request,
        CancellationToken cancellationToken)
    {
        var surveyIds = request.Items.Select(x => x.SurveyId).Distinct().ToList();

        // Tracked: the successful items are mutated and saved together at the end.
        var surveys = await context.Surveys
            .Include(x => x.Assignments)
            .Where(x => surveyIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var templateIds = surveys.Values.Select(x => x.TemplateId).Distinct().ToList();
        var templates = await context.SurveyTemplates
            .Where(x => templateIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        // Parsed once per template rather than once per item: a crew's queue is usually thirty fills
        // of the same form.
        var definitions = new Dictionary<long, SurveyDefinition>();
        foreach (var (templateId, template) in templates)
        {
            definitions[templateId] = SurveyDefinitionParser.Parse(template.DefinitionJson);
        }

        // Schema reconciliation is DDL and runs once per template, before the transaction opens —
        // adding a column inside the run's transaction would hold a schema lock for its duration.
        foreach (var template in templates.Values)
        {
            await submissionStore.ReconcileTableAsync(template, cancellationToken);
        }

        var alreadyRecorded = await LoadAlreadyRecordedAsync(request, surveys, cancellationToken);

        var filledByRole = user.IsInRole(FsmsRoles.FieldTeam) ? FilledByRoles.FieldTeam : FilledByRoles.BackOffice;
        var filledAt = timeProvider.GetUtcNow();

        var results = new List<BulkSubmissionItemResultDto>(request.Items.Count);
        var succeeded = 0;
        var duplicates = 0;

        // The store writes through native SQL, outside EF's change tracker, so its inserts would
        // otherwise be committed before the aggregates they belong to. One transaction over both is
        // what keeps a failed run from leaving submission rows whose surveys never advanced — and
        // those rows would be indistinguishable from accepted work on the next replay.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        foreach (var item in request.Items)
        {
            if (!surveys.TryGetValue(item.SurveyId, out var survey))
            {
                results.Add(Failure(item, null, "Survey not found."));
                continue;
            }

            if (alreadyRecorded.TryGetValue(item.ClientSubmissionId, out var existingId))
            {
                duplicates++;
                results.Add(new BulkSubmissionItemResultDto
                {
                    ClientSubmissionId = item.ClientSubmissionId,
                    SurveyId = item.SurveyId,
                    SurveyCode = survey.SurveyCode,
                    SubmissionId = existingId,
                    Success = true,
                    Duplicate = true,
                });
                continue;
            }

            var rejection = Validate(item, survey, templates, definitions, out var template, out var assignmentId);
            if (rejection is not null)
            {
                results.Add(Failure(item, survey.SurveyCode, rejection));
                continue;
            }

            // The domain refuses a survey that can no longer take a fill. Catching it per item keeps
            // one closed survey from taking the rest of the crew's queue down with it.
            try
            {
                var definition = definitions[template!.Id];

                // Re-keyed onto the trimmed names the definition is read under, before anything
                // matches answers against it — see SurveyAnswerKeys.
                var answers = SurveyAnswerKeys.Normalize(item.Answers);

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
                        SurveyId = item.SurveyId,
                        AssignmentId = assignmentId,
                        FilledByRole = filledByRole,
                        ClientSubmissionId = item.ClientSubmissionId,
                        Answers = answers,
                    },
                    cancellationToken);

                await SubmissionMediaLinker.LinkAsync(
                    context,
                    fileStorage,
                    definition,
                    answers,
                    template.Id,
                    submissionId,
                    survey.Id,
                    survey.SurveyCode,
                    cancellationToken);

                survey.RecordFill(filledByRole, user.Id, filledAt, assignmentId);
                survey.MergeSummary(
                    filledByRole,
                    SurveyFillSummary.Build(definition, answers, submissionId, filledByRole, user.Id, filledAt));

                succeeded++;
                results.Add(new BulkSubmissionItemResultDto
                {
                    ClientSubmissionId = item.ClientSubmissionId,
                    SurveyId = item.SurveyId,
                    SurveyCode = survey.SurveyCode,
                    SubmissionId = submissionId,
                    Success = true,
                });
            }
            catch (DomainException exception)
            {
                results.Add(Failure(item, survey.SurveyCode, exception.Message));
            }
        }

        if (succeeded > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return Result<BulkSubmissionResultDto>.Success(new BulkSubmissionResultDto
        {
            SucceededCount = succeeded,
            DuplicateCount = duplicates,
            FailedCount = results.Count - succeeded - duplicates,
            Results = results,
        });
    }

    /// <summary>
    /// Everything that would make this fill impossible, as a message — or null when it may proceed,
    /// in which case the template and the allocation it answers come back through the out
    /// parameters.
    /// </summary>
    private string? Validate(
        BulkSubmitSurveyItem item,
        Survey survey,
        IReadOnlyDictionary<long, SurveyTemplate> templates,
        IReadOnlyDictionary<long, SurveyDefinition> definitions,
        out SurveyTemplate? template,
        out long? assignmentId)
    {
        template = null;
        assignmentId = null;

        if (item.TemplateId is long requestedTemplateId && survey.TemplateId != requestedTemplateId)
        {
            return "The survey does not belong to this template.";
        }

        if (user.FieldTeamId is long fieldTeamId)
        {
            // A crew fills its own allocation and nothing else, exactly as on a single submit.
            var assignment = survey.Assignments
                .Where(a => a.FieldTeamId == fieldTeamId && a.IsActive)
                .OrderByDescending(a => a.AssignedDate)
                .FirstOrDefault();

            if (assignment is null)
            {
                return "This survey is not allocated to your field team.";
            }

            assignmentId = assignment.Id;
        }

        if (!templates.TryGetValue(survey.TemplateId, out var found))
        {
            return "Template not found.";
        }

        if (found.Status != TemplateStatuses.Published)
        {
            return "Only a published template can accept submissions.";
        }

        if (definitions[found.Id].Fields.Count == 0)
        {
            return "The template has no fields to submit against.";
        }

        // The same rule set a single submit applies. Reported as one message so the item fails on
        // its own and the rest of the crew's queue still lands — that is the whole point of a bulk
        // run, and a broken fill should cost the crew one form, not thirty.
        var answerErrors = SurveyAnswerValidator.Validate(
            definitions[found.Id],
            SurveyAnswerKeys.Normalize(item.Answers),
            ResolveFillClock(item.ClientFilledAt),
            surveyValidationOptions.Value.EnforceFieldRules);

        if (answerErrors.Count > 0)
        {
            return string.Join(" ", answerErrors.Select(error => error.Message));
        }

        template = found;
        return null;
    }

    /// <summary>
    /// The local wall clock this item's date rules are judged against. See the same method on
    /// <c>SubmitSurveyCommandHandler</c> — a client-stated fill time is honoured so a queue that
    /// drained late is judged on the day the work was done, but never later than the server's own
    /// clock.
    /// </summary>
    private DateTime ResolveFillClock(DateTimeOffset? clientFilledAt)
    {
        var branch = surveyTimeOptions.Value.ResolveTimeZone();
        var serverNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), branch).DateTime;

        if (clientFilledAt is not { } filledAt)
        {
            return serverNow;
        }

        var clientNow = TimeZoneInfo.ConvertTime(filledAt, branch).DateTime;
        return clientNow < serverNow ? clientNow : serverNow;
    }

    /// <summary>
    /// The client keys in this run that already carry a submission, in one read per template rather
    /// than one per item.
    /// </summary>
    private async Task<Dictionary<Guid, long>> LoadAlreadyRecordedAsync(
        BulkSubmitSurveysCommand request,
        IReadOnlyDictionary<long, Survey> surveys,
        CancellationToken cancellationToken)
    {
        var keysByTemplate = new Dictionary<long, List<Guid>>();

        foreach (var item in request.Items)
        {
            if (!surveys.TryGetValue(item.SurveyId, out var survey))
            {
                continue;
            }

            if (!keysByTemplate.TryGetValue(survey.TemplateId, out var keys))
            {
                keys = [];
                keysByTemplate[survey.TemplateId] = keys;
            }

            keys.Add(item.ClientSubmissionId);
        }

        var recorded = new Dictionary<Guid, long>();

        foreach (var (templateId, keys) in keysByTemplate)
        {
            var found = await submissionStore.FindByClientIdsAsync(templateId, keys, cancellationToken);
            foreach (var (clientSubmissionId, submissionId) in found)
            {
                recorded[clientSubmissionId] = submissionId;
            }
        }

        return recorded;
    }

    private static BulkSubmissionItemResultDto Failure(BulkSubmitSurveyItem item, string? surveyCode, string message) => new()
    {
        ClientSubmissionId = item.ClientSubmissionId,
        SurveyId = item.SurveyId,
        SurveyCode = surveyCode,
        Success = false,
        Message = message,
    };
}
