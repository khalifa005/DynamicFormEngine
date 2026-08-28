using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Surveys.Common;
using KH.Application.Fsms.Surveys.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Surveys.Queries.GetSurveyById;

/// <summary>
/// One survey with the definition frozen at its pinned version and the roll-up of every fill.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewSurveys)]
public record GetSurveyByIdQuery : IRequest<Result<SurveyDetailDto>>
{
    public long SurveyId { get; init; }
}

public sealed class GetSurveyByIdQueryValidator : AbstractValidator<GetSurveyByIdQuery>
{
    public GetSurveyByIdQueryValidator()
    {
        RuleFor(x => x.SurveyId)
            .GreaterThan(0).WithMessage("Survey id is required.");
    }
}

public sealed class GetSurveyByIdQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<GetSurveyByIdQuery, Result<SurveyDetailDto>>
{
    public async Task<Result<SurveyDetailDto>> Handle(GetSurveyByIdQuery request, CancellationToken cancellationToken)
    {
        var survey = await context.Surveys
            .AsNoTracking()
            .IncludeDetail()
            .FirstOrDefaultAsync(x => x.Id == request.SurveyId, cancellationToken);

        if (survey is null)
        {
            return Result<SurveyDetailDto>.Fail("Survey not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        // The detail screen renders who filled and who acted, so it asks for the names behind the ids.
        return Result<SurveyDetailDto>.Success(
            await survey.ToDetailDtoAsync(context, cancellationToken, identityService));
    }
}
