using KH.Application.Auth.Models;
using KH.Application.Common.Interfaces;

namespace KH.Application.Auth.Commands.ExchangeSsoCode;

/// <summary>
/// Trades the single-use code from an SSO redirect for a session. Returns the same
/// <see cref="AuthTokenDto"/> a password login does, so nothing downstream has to know which door
/// the caller came through.
/// </summary>
public record ExchangeSsoCodeCommand : IRequest<Result<AuthTokenDto>>
{
    public string Code { get; init; } = default!;
}

public sealed class ExchangeSsoCodeCommandHandler(IAuthService authService)
    : IRequestHandler<ExchangeSsoCodeCommand, Result<AuthTokenDto>>
{
    public Task<Result<AuthTokenDto>> Handle(ExchangeSsoCodeCommand request, CancellationToken cancellationToken) =>
        authService.ExchangeSsoCodeAsync(request.Code, cancellationToken);
}
