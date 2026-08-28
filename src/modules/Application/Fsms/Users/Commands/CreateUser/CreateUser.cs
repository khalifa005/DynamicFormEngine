using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Users.Common;
using KH.Application.Fsms.Users.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Users.Commands.CreateUser;

[Authorize(Policy = FsmsPolicies.ManageUsers)]
public record CreateUserCommand : IRequest<Result<UserDetailDto>>
{
    public string UserName { get; init; } = default!;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string Password { get; init; } = default!;

    /// <summary>
    /// Back-office roles only. A crew login is raised from the teams screen together with its team —
    /// see <c>CreateTeamCommand</c> — because a crew that exists as an account but not as a team,
    /// or the reverse, is a half-made thing somebody has to notice and finish by hand.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// What this account may see, from <c>ORG_SCOPES</c> — each row pairing a territory with a
    /// department. Empty means everything.
    /// </summary>
    public IReadOnlyList<OrgScopeAssignment> Scopes { get; init; } = [];
}

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("User name is required.")
            .MaximumLength(100).WithMessage("User name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email is not a valid address.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");

        RuleFor(x => x.Roles)
            .Must(roles => !roles.Contains(FsmsRoles.FieldTeam, StringComparer.Ordinal))
            .WithMessage("A field-team login is created from the teams screen, not here.");

        // A scope row may name a territory, a department, or both, so neither half of the
        // territory is required here. That a row carries at least one of them, and that its
        // codes exist, is checked by IOrgScopeService.ValidateAsync, where the reference data is.
        RuleForEach(x => x.Scopes).ChildRules(scope =>
        {
            scope.RuleFor(x => x.Level)
                .Must(OrgScopeLevels.IsDefined).WithMessage("Scope level is not recognized.")
                .When(x => !string.IsNullOrWhiteSpace(x.Level));
        });
    }
}

public sealed class CreateUserCommandHandler(
    IUserAccountService accountService,
    IOrgScopeService orgScopeService)
    : IRequestHandler<CreateUserCommand, Result<UserDetailDto>>
{
    public async Task<Result<UserDetailDto>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var created = await accountService.CreateUserAsync(
            new NewUserAccount
            {
                UserName = request.UserName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Password = request.Password,
                Roles = request.Roles
            },
            cancellationToken);

        if (!created.IsSuccess)
        {
            return Result<UserDetailDto>.Fail(created.Errors);
        }

        var userId = created.Data!;

        var scopeResult = await orgScopeService.ReplaceScopesAsync(
            OrgScopeOwnerTypes.User, userId, request.Scopes, cancellationToken);

        if (!scopeResult.IsSuccess)
        {
            return Result<UserDetailDto>.Fail(scopeResult.Errors);
        }

        var account = await accountService.GetUserAsync(userId, cancellationToken);

        if (!account.IsSuccess)
        {
            return Result<UserDetailDto>.Fail(account.Errors);
        }

        return Result<UserDetailDto>.Success(
            account.Data!.ToDetailDto(request.Scopes));
    }
}
