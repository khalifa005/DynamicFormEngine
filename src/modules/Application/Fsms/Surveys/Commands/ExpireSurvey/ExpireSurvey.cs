using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Surveys.Common;
using KH.Application.Fsms.Surveys.Models;
using KH.Domain.Constants;
using Shared.Core.Common;

namespace KH.Application.Fsms.Surveys.Commands.ExpireSurvey;

/// <summary>
/// Retires a survey whose deadline passed. Administrative, so it is role-gated rather than
/// permission-gated — expiring is a correction, not part of the normal flow.
/// </summary>
[Authorize(Roles = Roles.Administrator)]
public record ExpireSurveyCommand : IRequest<Result<SurveyDetailDto>>
{
    public long SurveyId { get; init; }
    public string? Note { get; init; }
}

public sealed class ExpireSurveyCommandValidator : AbstractValidator<ExpireSurveyCommand>
{
    public ExpireSurveyCommandValidator()
    {
        RuleFor(x => x.SurveyId)
            .GreaterThan(0).WithMessage("Survey id is required.");

        RuleFor(x => x.Note)
            .MaximumLength(1000).WithMessage("Note must not exceed 1000 characters.");
    }
}

public sealed class ExpireSurveyCommandHandler(
    IApplicationDbContext context,
    IUser user,
    TimeProvider timeProvider)
    : IRequestHandler<ExpireSurveyCommand, Result<SurveyDetailDto>>
{
    public async Task<Result<SurveyDetailDto>> Handle(ExpireSurveyCommand request, CancellationToken cancellationToken)
    {
        var survey = await context.Surveys
            .IncludeDetail()
            .FirstOrDefaultAsync(x => x.Id == request.SurveyId, cancellationToken);

        if (survey is null)
        {
            return Result<SurveyDetailDto>.Fail("Survey not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        survey.Expire(user.Id, timeProvider.GetUtcNow(), request.Note);

        await context.SaveChangesAsync(cancellationToken);

        return Result<SurveyDetailDto>.Success(await survey.ToDetailDtoAsync(context, cancellationToken));
    }
}
