using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Users.Commands.ResetUserPassword;

[Authorize(Policy = FsmsPolicies.ManageUsers)]
public record ResetUserPasswordCommand : IRequest<Result<string>>
{
    public string UserId { get; init; } = default!;
    public string NewPassword { get; init; } = default!;
}

public sealed class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User id is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}

public sealed class ResetUserPasswordCommandHandler(IUserAccountService accountService)
    : IRequestHandler<ResetUserPasswordCommand, Result<string>>
{
    public async Task<Result<string>> Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken) =>
        await accountService.ResetPasswordAsync(request.UserId, request.NewPassword, cancellationToken);
}
