using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Entities.Fsms.Teams;
using Shared.Core.Common;

namespace KH.Application.Fsms.Teams.Commands.UpdateFieldTeamLastActive;

/// <summary>
/// The mobile crew's periodic ping — same device and last-active location fields as team-login,
/// written onto the caller's own team. The team is taken from the token; the app can call this on
/// a timer (for example every hour) without signing in again.
/// </summary>
[Authorize]
public record UpdateFieldTeamLastActiveCommand : IRequest<Result<bool>>
{
    public string? DeviceName { get; init; }

    public string? Uuid { get; init; }

    public string? Version { get; init; }

    public string? Os { get; init; }

    public double? LastActiveLatitude { get; init; }

    public double? LastActiveLongitude { get; init; }
}

public sealed class UpdateFieldTeamLastActiveCommandValidator : AbstractValidator<UpdateFieldTeamLastActiveCommand>
{
    public UpdateFieldTeamLastActiveCommandValidator()
    {
        RuleFor(x => x.DeviceName)
            .MaximumLength(FieldTeam.DeviceNameMaxLength)
            .WithMessage($"Device name must not exceed {FieldTeam.DeviceNameMaxLength} characters.");

        RuleFor(x => x.Uuid)
            .MaximumLength(FieldTeam.DeviceUuidMaxLength)
            .WithMessage($"Device uuid must not exceed {FieldTeam.DeviceUuidMaxLength} characters.");

        RuleFor(x => x.Version)
            .MaximumLength(FieldTeam.AppVersionMaxLength)
            .WithMessage($"App version must not exceed {FieldTeam.AppVersionMaxLength} characters.");

        RuleFor(x => x.Os)
            .MaximumLength(FieldTeam.DeviceOsMaxLength)
            .WithMessage($"OS must not exceed {FieldTeam.DeviceOsMaxLength} characters.");

        RuleFor(x => x.LastActiveLatitude)
            .InclusiveBetween(-90, 90)
            .WithMessage("Last active latitude must be between -90 and 90.")
            .When(x => x.LastActiveLatitude.HasValue);

        RuleFor(x => x.LastActiveLongitude)
            .InclusiveBetween(-180, 180)
            .WithMessage("Last active longitude must be between -180 and 180.")
            .When(x => x.LastActiveLongitude.HasValue);

        RuleFor(x => x)
            .Must(x => x.LastActiveLatitude.HasValue == x.LastActiveLongitude.HasValue)
            .WithMessage("Last active latitude and longitude must be sent together.");
    }
}

public sealed class UpdateFieldTeamLastActiveCommandHandler(
    IApplicationDbContext context,
    IUser user,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateFieldTeamLastActiveCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        UpdateFieldTeamLastActiveCommand request,
        CancellationToken cancellationToken)
    {
        if (user.FieldTeamId is not long fieldTeamId)
        {
            return Result<bool>.Fail(
                "This endpoint is only available to a field-team login.",
                ApiErrorCodes.UnauthorizedAccess,
                httpStatusCode: 403);
        }

        var team = await context.FieldTeams
            .FirstOrDefaultAsync(t => t.Id == fieldTeamId && t.IsActive, cancellationToken);

        if (team is null)
        {
            return Result<bool>.Fail(
                "The field team linked to this login is no longer active.",
                ApiErrorCodes.NotFound,
                httpStatusCode: 404);
        }

        team.RecordDeviceSession(
            request.DeviceName,
            request.Uuid,
            request.Version,
            request.Os,
            request.LastActiveLatitude,
            request.LastActiveLongitude,
            timeProvider.GetUtcNow());

        await context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
