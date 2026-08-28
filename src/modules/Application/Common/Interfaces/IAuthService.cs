using KH.Application.Auth.Models;
using Shared.Core.Common;

namespace KH.Application.Common.Interfaces;

public interface IAuthService
{
    Task<Result<AuthTokenDto>> LoginAsync(string userName, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Signs a field crew in by its WFM user code. Only an account linked to a field team can come
    /// through here — a back-office login is rejected even with the right password.
    /// </summary>
    Task<Result<AuthTokenDto>> LoginFieldTeamAsync(string userCode, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Completes a corporate SSO sign-in for an identity OAM has already authenticated, and returns a
    /// single-use authorization code the browser trades for a token pair through
    /// <see cref="ExchangeSsoCodeAsync"/>.
    ///
    /// <paramref name="nameId"/> must match an existing account's user name — SSO never creates one.
    /// Every rule password login enforces (lockout, linked team still active, territory assigned)
    /// applies here too, so federation cannot be used to walk around a gate.
    /// </summary>
    Task<Result<SsoSignInResultDto>> SignInWithSamlAsync(
        string nameId,
        string? sessionIndex,
        CancellationToken cancellationToken);

    /// <summary>
    /// Trades an authorization code from <see cref="SignInWithSamlAsync"/> for the same
    /// <see cref="AuthTokenDto"/> a password login returns. The code is single-use and short-lived;
    /// spending it twice fails.
    /// </summary>
    Task<Result<AuthTokenDto>> ExchangeSsoCodeAsync(string code, CancellationToken cancellationToken);

    Task<Result<AuthTokenDto>> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>
    /// Ends the caller's session by revoking refresh tokens: the one named by
    /// <paramref name="refreshToken"/>, or — when none is given — every active token the account
    /// holds, which is what signs a device out of all its sessions. Already-issued access tokens are
    /// not recalled; they die at their own expiry.
    ///
    /// Idempotent, and deliberately silent about what it found: a token that does not exist, has
    /// already been revoked, or belongs to somebody else all answer the same way, so the endpoint
    /// cannot be used to probe for live tokens.
    /// </summary>
    Task<Result<bool>> LogoutAsync(string userId, string? refreshToken, CancellationToken cancellationToken);
}
