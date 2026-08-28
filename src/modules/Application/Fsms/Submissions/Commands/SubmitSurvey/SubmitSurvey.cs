using KH.Application.Common.Interfaces;
using KH.Application.Common.Options;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Definition;
using KH.Application.Fsms.Submissions.Common;
using KH.Application.Fsms.Submissions.Interfaces;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Common;

namespace KH.Application.Fsms.Submissions.Commands.SubmitSurvey;

/// <summary>
/// Records one fill of a survey. Open to reviewers as well as surveyors: a survey is filled from
/// both sides — the field team answers its part on site, the back office answers its own afterwards
/// — and the handler stamps each row with the role behind it.
/// </summary>
[Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
public record SubmitSurveyCommand : IRequest<Result<long>>
{
    /// <summary>
    /// Cross-checks the survey's own template. Optional: the survey already names its template, so
    /// a mobile client filling allocated work does not have to carry the id around. When supplied it
    /// must agree with the survey, which catches a client filling the wrong form.
    /// </summary>
    public long? TemplateId { get; init; }

    /// <summary>
    /// The survey instance being filled. Every fill answers a survey — a template on its own is a
    /// form definition, not work to be carried out, so there is nothing to record a fill against.
    /// </summary>
    public long SurveyId { get; init; }

    /// <summary>
    /// The field-team allocation this fill answers, when the survey has one. Ignored for a
    /// field-team login: that caller's allocation is resolved from its token instead, so a crew
    /// cannot post a fill against another crew's assignment.
    /// </summary>
    public long? AssignmentId { get; init; }

    /// <summary>
    /// The client's own key for this fill. A mobile app working offline generates it once, when the
    /// crew saves the form on the device, and sends the same value on every retry: posting twice
    /// under one key answers with the first submission's id rather than filling the survey again.
    /// Omit it and every post counts as a separate fill, which is what a form filled straight
    /// against the API wants.
    /// </summary>
    public Guid? ClientSubmissionId { get; init; }

    /// <summary>
    /// When the crew actually filled the form, in their own local time.
    ///
    /// Only date rules read it, and only to be fair to an offline client: a fill saved on a device
    /// on Monday under a "today or later" rule is still a Monday answer when it syncs on Wednesday,
    /// and judging it against Wednesday's clock would reject work that was correct when it was done.
    /// Clamped to the server clock, so a client cannot buy itself leniency by dating a fill forward.
    /// Omit it and the rules are judged against the moment the post arrives.
    /// </summary>
    public DateTimeOffset? ClientFilledAt { get; init; }

    /// <summary>Answers keyed by field <c>data_name</c>. Unknown keys are ignored by the store.</summary>
    public Dictionary<string, object?> Answers { get; init; } = new();
}

public sealed class SubmitSurveyCommandValidator : AbstractValidator<SubmitSurveyCommand>
{
    public SubmitSurveyCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .GreaterThan(0).WithMessage("Template id must be greater than 0.")
            .When(x => x.TemplateId.HasValue);

        RuleFor(x => x.SurveyId)
            .GreaterThan(0).WithMessage("Survey id is required.");

        RuleFor(x => x.AssignmentId)
            .GreaterThan(0).WithMessage("Assignment id must be greater than 0.")
            .When(x => x.AssignmentId.HasValue);

        RuleFor(x => x.ClientSubmissionId)
            .NotEqual(Guid.Empty).WithMessage("Client submission id must not be empty.")
            .When(x => x.ClientSubmissionId.HasValue);

        RuleFor(x => x.Answers)
            .NotNull().WithMessage("Answers are required.")
            .Must(a => a is { Count: > 0 }).WithMessage("At least one answer is required.");
    }
}

public sealed class SubmitSurveyCommandHandler(
    IApplicationDbContext context,
    ISurveySubmissionStore submissionStore,
    IUser user,
    TimeProvider timeProvider,
    IFileStorage fileStorage,
    IOptions<FileStorageOptions> fileStorageOptions,
    IOptions<SurveyTimeOptions> surveyTimeOptions,
    IOptions<SurveyValidationOptions> surveyValidationOptions)
    : IRequestHandler<SubmitSurveyCommand, Result<long>>
{
    public async Task<Result<long>> Handle(SubmitSurveyCommand request, CancellationToken cancellationToken)
    {
        // The survey is loaded first because it, not the request, is the authority on which template
        // is being filled — a client-supplied template id is only ever a cross-check.
        var survey = await context.Surveys
            .Include(x => x.Assignments)
            .FirstOrDefaultAsync(x => x.Id == request.SurveyId, cancellationToken);

        if (survey is null)
        {
            return Result<long>.Fail("Survey not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        if (request.TemplateId is long requestedTemplateId && survey.TemplateId != requestedTemplateId)
        {
            return Result<long>.Fail(
                "The survey does not belong to this template.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        // A replay of a fill already accepted. Answering with the original id — rather than 409 —
        // is what lets an offline client treat a lost response and a successful one identically.
        if (request.ClientSubmissionId is Guid clientSubmissionId)
        {
            var existingId = await submissionStore.FindByClientIdAsync(
                survey.TemplateId, clientSubmissionId, cancellationToken);

            if (existingId is long alreadyRecorded)
            {
                return Result<long>.Success(alreadyRecorded);
            }
        }

        long? assignmentId;

        if (user.FieldTeamId is long fieldTeamId)
        {
            // A crew fills its own allocation and nothing else. Taking the assignment from the token
            // rather than the body is what stops one crew posting a fill against another's work, and
            // it is why the mobile client never has to send an assignment id at all.
            var assignment = survey.Assignments
                .Where(a => a.FieldTeamId == fieldTeamId && a.IsActive)
                .OrderByDescending(a => a.AssignedDate)
                .FirstOrDefault();

            if (assignment is null)
            {
                return Result<long>.Fail(
                    "This survey is not allocated to your field team.",
                    ApiErrorCodes.UnauthorizedAccess,
                    httpStatusCode: 403);
            }

            assignmentId = assignment.Id;
        }
        else
        {
            // Back office: a fill may answer an allocation or none at all, but a named one has to
            // belong to this survey.
            if (request.AssignmentId is long requestedAssignmentId
                && survey.Assignments.All(a => a.Id != requestedAssignmentId))
            {
                return Result<long>.Fail(
                    "The assignment does not belong to this survey.",
                    ApiErrorCodes.ValidationError,
                    httpStatusCode: 400);
            }

            assignmentId = request.AssignmentId;
        }

        var template = await context.SurveyTemplates
            .FirstOrDefaultAsync(x => x.Id == survey.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result<long>.Fail("Template not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        if (template.Status != TemplateStatuses.Published)
        {
            return Result<long>.Fail(
                "Only a published template can accept submissions.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        if (!SurveyDefinitionParser.HasElements(template.DefinitionJson))
        {
            return Result<long>.Fail(
                "The template has no fields to submit against.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        // A survey is filled by both sides: the field team on site and the back office afterwards.
        var filledByRole = user.IsInRole(FsmsRoles.FieldTeam) ? FilledByRoles.FieldTeam : FilledByRoles.BackOffice;

        // Ensure the shared table has this template's columns, then insert.
        await submissionStore.ReconcileTableAsync(template, cancellationToken);

        var definition = SurveyDefinitionParser.Parse(template.DefinitionJson);

        // Re-keyed onto the trimmed names the definition is read under, before anything matches
        // answers against it — see SurveyAnswerKeys.
        var answers = SurveyAnswerKeys.Normalize(request.Answers);

        // The fill form applies every one of these before it lets the crew submit, but the API also
        // takes posts from the mobile app and from anything holding a token, so a rule is only a
        // rule once it is checked here.
        var answerErrors = SurveyAnswerValidator.Validate(
            definition,
            answers,
            ResolveFillClock(request),
            surveyValidationOptions.Value.EnforceFieldRules);

        if (answerErrors.Count > 0)
        {
            return Result<long>.Fail(answerErrors
                .Select(error => new ErrorInfo(error.Message, ApiErrorCodes.ValidationError, httpStatusCode: 400))
                .ToList());
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

        var id = await submissionStore.InsertAsync(
            new SubmissionInsert
            {
                TemplateId = template.Id,
                VersionNo = survey.TemplateVersionNo ?? template.CurrentVersionNo,
                SubmittedBy = user.Id,
                SurveyId = request.SurveyId,
                AssignmentId = assignmentId,
                FilledByRole = filledByRole,
                ClientSubmissionId = request.ClientSubmissionId,
                Answers = answers,
            },
            cancellationToken);

        await SubmissionMediaLinker.LinkAsync(
            context, fileStorage, definition, answers, template.Id, id, survey.Id, survey.SurveyCode, cancellationToken);

        var filledAt = timeProvider.GetUtcNow();

        survey.RecordFill(filledByRole, user.Id, filledAt, assignmentId);
        survey.MergeSummary(
            filledByRole,
            SurveyFillSummary.Build(definition, answers, id, filledByRole, user.Id, filledAt));

        await context.SaveChangesAsync(cancellationToken);

        return Result<long>.Success(id);
    }

    /// <summary>
    /// The local wall clock a date rule is judged against.
    ///
    /// Local, not UTC: an answer is a calendar value the crew read off a phone set to local time,
    /// and three hours of offset move "today" for every fill after 21:00 Riyadh. A client-supplied
    /// fill time is honoured so an offline sync is judged on the day the work was done, but never
    /// later than the server's own clock — otherwise a client could date a fill forward and walk
    /// past a "must not be in the future" rule.
    /// </summary>
    private DateTime ResolveFillClock(SubmitSurveyCommand request)
    {
        var branch = surveyTimeOptions.Value.ResolveTimeZone();
        var serverNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), branch).DateTime;

        if (request.ClientFilledAt is not { } filledAt)
        {
            return serverNow;
        }

        var clientNow = TimeZoneInfo.ConvertTime(filledAt, branch).DateTime;
        return clientNow < serverNow ? clientNow : serverNow;
    }
}
