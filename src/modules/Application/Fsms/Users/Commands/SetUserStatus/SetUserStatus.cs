using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Users.Commands.SetUserStatus;

[Authorize(Policy = FsmsPolicies.ManageUsers)]
public record SetUserStatusCommand : IRequest<Result<string>>
{
    public string UserId { get; init; } = default!;
    public bool IsEnabled { get; init; }
}

public sealed class SetUserStatusCommandValidator : AbstractValidator<SetUserStatusCommand>
{
    public SetUserStatusCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User id is required.");
    }
}

public sealed class SetUserStatusCommandHandler(IUserAccountService accountService)
    : IRequestHandler<SetUserStatusCommand, Result<string>>
{
    public async Task<Result<string>> Handle(SetUserStatusCommand request, CancellationToken cancellationToken) =>
        await accountService.SetUserEnabledAsync(request.UserId, request.IsEnabled, cancellationToken);
}
