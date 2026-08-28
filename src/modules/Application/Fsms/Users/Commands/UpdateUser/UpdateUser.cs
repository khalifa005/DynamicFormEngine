using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Users.Common;
using KH.Application.Fsms.Users.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Users.Commands.UpdateUser;

[Authorize(Policy = FsmsPolicies.ManageUsers)]
public record UpdateUserCommand : IRequest<Result<UserDetailDto>>
{
    public string UserId { get; init; } = default!;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }

    /// <summary>Back-office roles only — a crew login is managed from the teams screen.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// What this account may see, from <c>ORG_SCOPES</c> — each row pairing a territory with a
    /// department. Empty means everything.
    /// </summary>
    public IReadOnlyList<OrgScopeAssignment> Scopes { get; init; } = [];
}

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User id is required.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email is not a valid address.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Roles)
            .Must(roles => !roles.Contains(FsmsRoles.FieldTeam, StringComparer.Ordinal))
            .WithMessage("A field-team login is managed from the teams screen, not here.");

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

public sealed class UpdateUserCommandHandler(
    IUserAccountService accountService,
    IOrgScopeService orgScopeService)
    : IRequestHandler<UpdateUserCommand, Result<UserDetailDto>>
{
    public async Task<Result<UserDetailDto>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var updated = await accountService.UpdateUserAsync(
            request.UserId,
            new EditedUserAccount
            {
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Roles = request.Roles
            },
            cancellationToken);

        if (!updated.IsSuccess)
        {
            return Result<UserDetailDto>.Fail(updated.Errors);
        }

        var scopeResult = await orgScopeService.ReplaceScopesAsync(
            OrgScopeOwnerTypes.User, request.UserId, request.Scopes, cancellationToken);

        if (!scopeResult.IsSuccess)
        {
            return Result<UserDetailDto>.Fail(scopeResult.Errors);
        }

        var account = await accountService.GetUserAsync(request.UserId, cancellationToken);

        if (!account.IsSuccess)
        {
            return Result<UserDetailDto>.Fail(account.Errors);
        }

        return Result<UserDetailDto>.Success(
            account.Data!.ToDetailDto(request.Scopes));
    }
}
