using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Surveys.Common;
using KH.Application.Fsms.Surveys.Models;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Surveys;
using Shared.Core.Common;

namespace KH.Application.Fsms.Surveys.Commands.CompleteSurvey;

/// <summary>Closes a filled survey: <c>SUBMITTED</c> -> <c>APPROVED</c> (COMPLETED).</summary>
[Authorize(Policy = FsmsPolicies.ReviewSurveys)]
public record CompleteSurveyCommand : IRequest<Result<SurveyDetailDto>>
{
    public long SurveyId { get; init; }
    public string? Note { get; init; }
}

public sealed class CompleteSurveyCommandValidator : AbstractValidator<CompleteSurveyCommand>
{
    public CompleteSurveyCommandValidator()
    {
        RuleFor(x => x.SurveyId)
            .GreaterThan(0).WithMessage("Survey id is required.");

        RuleFor(x => x.Note)
            .MaximumLength(1000).WithMessage("Note must not exceed 1000 characters.");
    }
}

public sealed class CompleteSurveyCommandHandler(
    IApplicationDbContext context,
    IUser user,
    TimeProvider timeProvider)
    : IRequestHandler<CompleteSurveyCommand, Result<SurveyDetailDto>>
{
    public async Task<Result<SurveyDetailDto>> Handle(CompleteSurveyCommand request, CancellationToken cancellationToken)
    {
        var survey = await context.Surveys
            .IncludeDetail()
            .FirstOrDefaultAsync(x => x.Id == request.SurveyId, cancellationToken);

        if (survey is null)
        {
            return Result<SurveyDetailDto>.Fail("Survey not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        if (survey.Status is not (SurveyStatuses.Submitted or SurveyStatuses.UnderReview))
        {
            return Result<SurveyDetailDto>.Fail(
                $"Only a {SurveyStatuses.Submitted} survey can be completed (current: {survey.Status}).",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        survey.Complete(user.Id, timeProvider.GetUtcNow(), request.Note);
        await context.SaveChangesAsync(cancellationToken);

        return Result<SurveyDetailDto>.Success(await survey.ToDetailDtoAsync(context, cancellationToken));
    }
}
