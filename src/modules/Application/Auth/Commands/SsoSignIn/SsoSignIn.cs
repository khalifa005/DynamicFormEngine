using KH.Application.Auth.Models;
using KH.Application.Common.Interfaces;

namespace KH.Application.Auth.Commands.SsoSignIn;

/// <summary>
/// Completes a corporate SSO sign-in for an identity the SAML handler has already validated. The
/// caller is trusted to have authenticated the assertion — this slice only decides whether FSMS
/// admits that identity, and mints the code the browser trades for a session.
/// </summary>
public record SsoSignInCommand : IRequest<Result<SsoSignInResultDto>>
{
    /// <summary>The assertion's NameID, which must equal an existing account's user name.</summary>
    public string NameId { get; init; } = default!;

    /// <summary>The IdP session this sign-in belongs to, when the assertion carried one.</summary>
    public string? SessionIndex { get; init; }
}

public sealed class SsoSignInCommandHandler(IAuthService authService)
    : IRequestHandler<SsoSignInCommand, Result<SsoSignInResultDto>>
{
    public Task<Result<SsoSignInResultDto>> Handle(SsoSignInCommand request, CancellationToken cancellationToken) =>
        authService.SignInWithSamlAsync(request.NameId, request.SessionIndex, cancellationToken);
}
