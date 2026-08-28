using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Surveys.Common;
using KH.Application.Fsms.Surveys.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Surveys.Commands.ReturnSurvey;

/// <summary>
/// Sends a filled survey back for rework: <c>SUBMITTED</c> -> <c>RETURNED</c>. The rework stays
/// with the crew that filled it unless the reviewer names a different one.
/// </summary>
[Authorize(Policy = FsmsPolicies.ReviewSurveys)]
public record ReturnSurveyCommand : IRequest<Result<SurveyDetailDto>>
{
    public long SurveyId { get; init; }

    /// <summary>
    /// A <c>LKP_RETURN_REASON</c> code. Structured so the crew's worklist can flag the survey at a
    /// glance and so returns can be reported on by cause.
    /// </summary>
    public string ReasonCode { get; init; } = default!;

    /// <summary>Why it goes back — the field team needs to know what to redo.</summary>
    public string Reason { get; init; } = default!;

    /// <summary>
    /// Hands the rework to a different crew. Null keeps it with the team that already holds it.
    /// </summary>
    public long? ReassignToFieldTeamId { get; init; }
}

public sealed class ReturnSurveyCommandValidator : AbstractValidator<ReturnSurveyCommand>
{
    public ReturnSurveyCommandValidator()
    {
        RuleFor(x => x.SurveyId)
            .GreaterThan(0).WithMessage("Survey id is required.");

        RuleFor(x => x.ReasonCode)
            .NotEmpty().WithMessage("A return reason code is required.")
            .MaximumLength(SurveyReturnReasons.MaxCodeLength)
            .WithMessage($"Return reason code must not exceed {SurveyReturnReasons.MaxCodeLength} characters.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A return reason is required.")
            .MaximumLength(1000).WithMessage("Return reason must not exceed 1000 characters.");

        RuleFor(x => x.ReassignToFieldTeamId)
            .GreaterThan(0).WithMessage("Field team id must be greater than zero.")
            .When(x => x.ReassignToFieldTeamId is not null);
    }
}

public sealed class ReturnSurveyCommandHandler(
    IApplicationDbContext context,
    IUser user,
    TimeProvider timeProvider)
    : IRequestHandler<ReturnSurveyCommand, Result<SurveyDetailDto>>
{
    public async Task<Result<SurveyDetailDto>> Handle(ReturnSurveyCommand request, CancellationToken cancellationToken)
    {
        var survey = await context.Surveys
            .IncludeDetail()
            .FirstOrDefaultAsync(x => x.Id == request.SurveyId, cancellationToken);

        if (survey is null)
        {
            return Result<SurveyDetailDto>.Fail("Survey not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        // Validated against the table rather than the shipped constants: the lookup is editable, so
        // a code support operations added later is just as valid as one we seeded.
        var reasonExists = await context.FsmsReturnReasons
            .AnyAsync(x => x.Code == request.ReasonCode && x.IsActive, cancellationToken);

        if (!reasonExists)
        {
            return Result<SurveyDetailDto>.Fail(
                "Return reason not found or inactive.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        if (request.ReassignToFieldTeamId is long targetTeamId)
        {
            var teamExists = await context.FieldTeams
                .AnyAsync(x => x.Id == targetTeamId && x.IsActive, cancellationToken);

            if (!teamExists)
            {
                return Result<SurveyDetailDto>.Fail(
                    "Field team not found or inactive.",
                    ApiErrorCodes.ValidationError,
                    httpStatusCode: 400);
            }
        }

        // Refused by the domain too; caught here so the reviewer gets a status they can act on
        // rather than the generic shape an unhandled DomainException produces.
        if (survey.Status is not (SurveyStatuses.Submitted or SurveyStatuses.UnderReview))
        {
            return Result<SurveyDetailDto>.Fail(
                $"Only a {SurveyStatuses.Submitted} survey can be returned (current: {survey.Status}).",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        survey.Return(
            user.Id,
            timeProvider.GetUtcNow(),
            request.ReasonCode,
            request.Reason,
            request.ReassignToFieldTeamId);

        await context.SaveChangesAsync(cancellationToken);

        return Result<SurveyDetailDto>.Success(await survey.ToDetailDtoAsync(context, cancellationToken));
    }
}
