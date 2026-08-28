using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Surveys.Common;
using KH.Application.Fsms.Surveys.Models;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Surveys;
using Shared.Core.Common;
using System.Text.Json.Serialization;

namespace KH.Application.Fsms.Surveys.Commands.UpdateSurvey;

/// <summary>
/// Corrects a survey's placement and the customer it is for — CBU, branch, operation area,
/// department, deadline, and the customer's name and phone number. Only while the survey is
/// unfilled: <see cref="Domain.Entities.Fsms.Surveys.Survey.SetLocation"/> refuses once any fill has
/// been recorded, for the same reason <c>Assign</c> does.
/// </summary>
/// <remarks>
/// Every field is a replacement, not a patch — an omitted property clears the stored value. The
/// dialog posts the whole document each time, so a caller building this by hand must send the
/// customer details it wants kept, not only the ones it is changing.
/// </remarks>
[Authorize(Policy = FsmsPolicies.ManageAssignments)]
public record UpdateSurveyCommand : IRequest<Result<SurveyDetailDto>>
{
    [JsonIgnore]
    public long SurveyId { get; init; }

    public string? CustomerName { get; init; }

    /// <summary>The number the crew calls before arriving.</summary>
    public string? CustomerPhone { get; init; }

    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public int? DepartmentId { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? CompletionDueDate { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? Note { get; init; }
}

public sealed class UpdateSurveyCommandValidator : AbstractValidator<UpdateSurveyCommand>
{
    public UpdateSurveyCommandValidator()
    {
        RuleFor(x => x.SurveyId)
            .GreaterThan(0).WithMessage("Survey id is required.");

        RuleFor(x => x.CustomerName)
            .MaximumLength(SurveyFieldLimits.CustomerName)
            .WithMessage($"Customer name must not exceed {SurveyFieldLimits.CustomerName} characters.");

        RuleFor(x => x.CustomerPhone)
            .MaximumLength(SurveyFieldLimits.CustomerPhone)
            .WithMessage($"Customer phone must not exceed {SurveyFieldLimits.CustomerPhone} characters.");

        RuleFor(x => x.CbuCode)
            .MaximumLength(50).WithMessage("CBU code must not exceed 50 characters.");

        RuleFor(x => x.BranchCode)
            .MaximumLength(50).WithMessage("Branch code must not exceed 50 characters.");

        RuleFor(x => x.OperationAreaCode)
            .MaximumLength(50).WithMessage("Operation area code must not exceed 50 characters.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).WithMessage("Latitude must be between -90 and 90.")
            .When(x => x.Latitude.HasValue);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).WithMessage("Longitude must be between -180 and 180.")
            .When(x => x.Longitude.HasValue);

        RuleFor(x => x.Note)
            .MaximumLength(1000).WithMessage("Note must not exceed 1000 characters.");
    }
}

public sealed class UpdateSurveyCommandHandler(
    IApplicationDbContext context,
    IUser user,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateSurveyCommand, Result<SurveyDetailDto>>
{
    public async Task<Result<SurveyDetailDto>> Handle(UpdateSurveyCommand request, CancellationToken cancellationToken)
    {
        var survey = await context.Surveys
            .IncludeDetail()
            .FirstOrDefaultAsync(x => x.Id == request.SurveyId, cancellationToken);

        if (survey is null)
        {
            return Result<SurveyDetailDto>.Fail("Survey not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        // Both are refused by the domain too; caught here so the dispatcher gets a status they can
        // act on rather than the generic shape an unhandled DomainException produces.
        if (survey.Status is not (SurveyStatuses.Created or SurveyStatuses.Assigned or SurveyStatuses.InProgress))
        {
            return Result<SurveyDetailDto>.Fail(
                $"A {survey.Status} survey cannot be relocated.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        if (survey.SubmissionCount > 0)
        {
            return Result<SurveyDetailDto>.Fail(
                "A survey that has already been filled cannot be relocated.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        var changedAt = timeProvider.GetUtcNow();

        // Left to write its own timeline note rather than being handed the dispatcher's: a changed
        // phone number is worth its own entry, and the note below already explains the relocation.
        // Silent when neither value moved, so an unchanged dialog adds nothing.
        survey.SetCustomerContact(request.CustomerName, request.CustomerPhone, user.Id, changedAt);

        survey.SetLocation(
            request.CbuCode,
            request.BranchCode,
            request.OperationAreaCode,
            request.DepartmentId,
            request.DueDate,
            user.Id,
            changedAt,
            request.Note,
            request.Latitude,
            request.Longitude,
            request.CompletionDueDate);

        await context.SaveChangesAsync(cancellationToken);

        return Result<SurveyDetailDto>.Success(await survey.ToDetailDtoAsync(context, cancellationToken));
    }
}
