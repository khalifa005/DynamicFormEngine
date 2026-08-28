using KH.Application.Auth.Models;
using KH.Application.Common.Interfaces;

namespace KH.Application.Auth.Commands.RefreshAccessToken;

public record RefreshAccessTokenCommand : IRequest<Result<AuthTokenDto>>
{
    public string RefreshToken { get; init; } = default!;
}

public sealed class RefreshAccessTokenCommandHandler(IAuthService authService)
    : IRequestHandler<RefreshAccessTokenCommand, Result<AuthTokenDto>>
{
    public Task<Result<AuthTokenDto>> Handle(RefreshAccessTokenCommand request, CancellationToken cancellationToken) =>
        authService.RefreshAsync(request.RefreshToken, cancellationToken);
}
