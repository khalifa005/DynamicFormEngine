using KH.Application.Auth.Models;
using KH.Application.Common.Interfaces;
using KH.Domain.Entities.Fsms.Teams;

namespace KH.Application.Auth.Commands.LoginFieldTeam;

/// <summary>
/// The mobile sign-in for a field crew. A crew knows its WFM user code, not an email, so the code
/// is what it signs in with; the token that comes back carries the team it belongs to.
/// Optional device and location fields are stored on the team and overwritten on every success.
/// </summary>
public record LoginFieldTeamCommand : IRequest<Result<AuthTokenDto>>
{
    /// <summary>The team's WFM user code (<c>TEAMS.UserCode</c>).</summary>
    public string UserCode { get; init; } = default!;

    public string Password { get; init; } = default!;

    public string? DeviceName { get; init; }

    public string? Uuid { get; init; }

    public string? Version { get; init; }

    public string? Os { get; init; }

    public double? LastActiveLatitude { get; init; }

    public double? LastActiveLongitude { get; init; }
}

public sealed class LoginFieldTeamCommandValidator : AbstractValidator<LoginFieldTeamCommand>
{
    public LoginFieldTeamCommandValidator()
    {
        RuleFor(x => x.UserCode)
            .NotEmpty().WithMessage("User code is required.")
            .MaximumLength(50).WithMessage("User code must not exceed 50 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");

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

public sealed class LoginFieldTeamCommandHandler(
    IAuthService authService,
    IApplicationDbContext context,
    TimeProvider timeProvider)
    : IRequestHandler<LoginFieldTeamCommand, Result<AuthTokenDto>>
{
    public async Task<Result<AuthTokenDto>> Handle(
        LoginFieldTeamCommand request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginFieldTeamAsync(
            request.UserCode,
            request.Password,
            cancellationToken);

        if (!result.IsSuccess || result.Data?.FieldTeamId is not long fieldTeamId)
        {
            return result;
        }

        var team = await context.FieldTeams
            .FirstOrDefaultAsync(t => t.Id == fieldTeamId, cancellationToken);

        if (team is null)
        {
            return result;
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

        return result;
    }
}
