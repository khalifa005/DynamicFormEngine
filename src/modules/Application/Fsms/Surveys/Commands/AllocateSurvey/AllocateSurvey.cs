using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Surveys.Common;
using KH.Application.Fsms.Surveys.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Surveys.Commands.AllocateSurvey;

/// <summary>
/// Allocates a survey to a field team. Runs as often as the work needs crews: an already-allocated
/// survey can be handed to a further team, and each call adds its own allocation. The domain decides
/// which statuses still accept one — see <see cref="KH.Domain.Entities.Fsms.Surveys.Survey.Assign"/>.
/// </summary>
[Authorize(Policy = FsmsPolicies.ManageAssignments)]
public record AllocateSurveyCommand : IRequest<Result<SurveyDetailDto>>
{
    public long SurveyId { get; init; }
    public long FieldTeamId { get; init; }

    /// <summary>Overrides the survey's fill deadline for this allocation when supplied.</summary>
    public DateTimeOffset? DueDate { get; init; }

    /// <summary>Overrides the survey's completion (approve) deadline when supplied.</summary>
    public DateTimeOffset? CompletionDueDate { get; init; }

    public string? Note { get; init; }
}

public sealed class AllocateSurveyCommandValidator : AbstractValidator<AllocateSurveyCommand>
{
    public AllocateSurveyCommandValidator()
    {
        RuleFor(x => x.SurveyId)
            .GreaterThan(0).WithMessage("Survey id is required.");

        RuleFor(x => x.FieldTeamId)
            .GreaterThan(0).WithMessage("Field team id is required.");

        RuleFor(x => x.Note)
            .MaximumLength(1000).WithMessage("Note must not exceed 1000 characters.");
    }
}

public sealed class AllocateSurveyCommandHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService,
    IUser user,
    TimeProvider timeProvider)
    : IRequestHandler<AllocateSurveyCommand, Result<SurveyDetailDto>>
{
    public async Task<Result<SurveyDetailDto>> Handle(AllocateSurveyCommand request, CancellationToken cancellationToken)
    {
        var survey = await context.Surveys
            .IncludeDetail()
            .FirstOrDefaultAsync(x => x.Id == request.SurveyId, cancellationToken);

        if (survey is null)
        {
            return Result<SurveyDetailDto>.Fail("Survey not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        var teamExists = await context.FieldTeams
            .AnyAsync(x => x.Id == request.FieldTeamId && x.IsActive, cancellationToken);

        if (!teamExists)
        {
            return Result<SurveyDetailDto>.Fail(
                "Field team not found or inactive.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        // The rule that makes the mapping mean something: a crew can only be handed work that
        // falls inside the territory it was scoped to.
        var teamScope = await orgScopeService.GetScopeAsync(
            OrgScopeOwnerTypes.Team, request.FieldTeamId.ToString(), cancellationToken);

        // Department as well as place: a water crew covering branch 1110 is still the wrong crew for
        // waste-water work in it.
        if (!teamScope.Covers(survey.CbuCode, survey.BranchCode, survey.OperationAreaCode, survey.DepartmentId))
        {
            return Result<SurveyDetailDto>.Fail(
                "This field team's territory does not cover the survey's location.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        // Both are refused by the domain too; caught here so the dispatcher gets a status they can
        // act on rather than the generic shape an unhandled DomainException produces.
        if (survey.SubmissionCount > 0)
        {
            return Result<SurveyDetailDto>.Fail(
                "A survey that has already been filled cannot be re-allocated.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        if (survey.Assignments.Any(a => a.IsActive && a.FieldTeamId == request.FieldTeamId))
        {
            return Result<SurveyDetailDto>.Fail(
                "The survey is already allocated to this field team.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        survey.Assign(
            request.FieldTeamId,
            user.Id,
            timeProvider.GetUtcNow(),
            request.DueDate,
            request.Note,
            request.CompletionDueDate);

        await context.SaveChangesAsync(cancellationToken);

        return Result<SurveyDetailDto>.Success(await survey.ToDetailDtoAsync(context, cancellationToken));
    }
}
