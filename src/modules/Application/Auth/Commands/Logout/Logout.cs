using KH.Application.Common.Interfaces;
using Shared.Core.Common;

namespace KH.Application.Auth.Commands.Logout;

/// <summary>
/// Ends the caller's session. Sent with the refresh token it was issued, only that session is
/// closed; sent empty, every session the account holds is — which is what the mobile app does when
/// a crew signs off a shared device.
///
/// The access token already in the client's hands is not recalled: it stays valid until it expires,
/// so the client must drop its copy. What logout guarantees is that no new access token can be
/// minted from the revoked refresh token.
/// </summary>
public record LogoutCommand : IRequest<Result<bool>>
{
    /// <summary>The session to close. Omit to close every session of the account.</summary>
    public string? RefreshToken { get; init; }
}

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    /// <summary>A refresh token is 64 random bytes base64-encoded; this leaves ample headroom.</summary>
    private const int MaxRefreshTokenLength = 512;

    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken!)
            .MaximumLength(MaxRefreshTokenLength)
            .WithMessage($"Refresh token must not exceed {MaxRefreshTokenLength} characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.RefreshToken));
    }
}

public sealed class LogoutCommandHandler(IAuthService authService, IUser user)
    : IRequestHandler<LogoutCommand, Result<bool>>
{
    public Task<Result<bool>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // The session closed is always the caller's own — the token is never taken as the identity,
        // so a stolen refresh token cannot be used to sign somebody else out.
        if (string.IsNullOrWhiteSpace(user.Id))
        {
            return Task.FromResult(Result<bool>.Fail(
                "Not authenticated.", ApiErrorCodes.UnauthorizedAccess, httpStatusCode: 401));
        }

        return authService.LogoutAsync(user.Id, request.RefreshToken, cancellationToken);
    }
}
