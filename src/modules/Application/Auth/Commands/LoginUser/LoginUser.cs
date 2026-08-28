using KH.Application.Auth.Models;
using KH.Application.Common.Interfaces;

namespace KH.Application.Auth.Commands.LoginUser;

public record LoginUserCommand : IRequest<Result<AuthTokenDto>>
{
    public string UserName { get; init; } = default!;

    public string Password { get; init; } = default!;
}

public sealed class LoginUserCommandHandler(IAuthService authService)
    : IRequestHandler<LoginUserCommand, Result<AuthTokenDto>>
{
    public Task<Result<AuthTokenDto>> Handle(LoginUserCommand request, CancellationToken cancellationToken) =>
        authService.LoginAsync(request.UserName, request.Password, cancellationToken);
}
