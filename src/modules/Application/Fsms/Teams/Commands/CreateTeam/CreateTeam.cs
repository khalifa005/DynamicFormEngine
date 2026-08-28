using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Teams;
using Shared.Core.Common;

namespace KH.Application.Fsms.Teams.Commands.CreateTeam;

[Authorize(Policy = FsmsPolicies.ManageTeams)]
public record CreateTeamCommand : IRequest<Result<long>>
{
    public string UserCode { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string? Mobile { get; init; }
    public string? TeamType { get; init; }

    public int? ContractorId { get; init; }

    /// <summary>
    /// Where the crew works and what kind of work is theirs — each row pairs a territory with a
    /// department, so a crew doing water in one branch and waste-water in another says exactly that.
    /// </summary>
    public IReadOnlyList<OrgScopeAssignment> Scopes { get; init; } = [];
    public bool IsActive { get; init; } = true;

    /// <summary>Contact address for the crew's login.</summary>
    public string? Email { get; init; }

    /// <summary>
    /// Password for the crew's login, created here alongside the team. A crew is a login as much as
    /// it is a record — one without the other is a team that cannot work — so the two are raised
    /// together rather than leaving someone to remember the second step on the users screen.
    /// The login is named after <see cref="UserCode"/>, which is what the mobile app signs in with.
    /// </summary>
    public string Password { get; init; } = default!;
}

public sealed class CreateTeamCommandValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamCommandValidator()
    {
        RuleFor(x => x.UserCode)
            .NotEmpty().WithMessage("User code is required.")
            .MaximumLength(50).WithMessage("User code must not exceed 50 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(250).WithMessage("Name must not exceed 250 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A password for the crew's login is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");

        RuleFor(x => x.ContractorId)
            .GreaterThan(0).WithMessage("Contractor id is not valid.")
            .When(x => x.ContractorId.HasValue);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email is not a valid address.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

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

public sealed class CreateTeamCommandHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService,
    IUserAccountService accountService)
    : IRequestHandler<CreateTeamCommand, Result<long>>
{
    public async Task<Result<long>> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var codeTaken = await context.FieldTeams
            .AnyAsync(x => x.UserCode == request.UserCode, cancellationToken);

        if (codeTaken)
        {
            return Result<long>.Fail(
                $"A team with user code '{request.UserCode}' already exists.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        if (request.ContractorId.HasValue)
        {
            var contractorExists = await context.FsmsContractors
                .AnyAsync(x => x.Id == request.ContractorId.Value, cancellationToken);

            if (!contractorExists)
            {
                return Result<long>.Fail(
                    "Contractor not found.",
                    ApiErrorCodes.ValidationError,
                    httpStatusCode: 400);
            }
        }

        var team = new FieldTeam
        {
            UserCode = request.UserCode,
            Name = request.Name,
            Mobile = request.Mobile,
            TeamType = request.TeamType,
            ContractorId = request.ContractorId,
            IsActive = request.IsActive
        };

        context.FieldTeams.Add(team);
        await context.SaveChangesAsync(cancellationToken);

        // The login can only be created once the team has an id to point at, so a rejected login —
        // a duplicate name, a password Identity refuses — leaves a team behind that cannot work.
        // Undo it rather than hand back a half-made crew the operator would have to clean up.
        var login = await accountService.CreateUserAsync(
            new NewUserAccount
            {
                UserName = request.UserCode,
                Email = request.Email,
                PhoneNumber = request.Mobile,
                Password = request.Password,
                FieldTeamId = team.Id,
                Roles = [FsmsRoles.FieldTeam]
            },
            cancellationToken);

        if (!login.IsSuccess)
        {
            context.FieldTeams.Remove(team);
            await context.SaveChangesAsync(cancellationToken);

            return Result<long>.Fail(login.Errors);
        }

        if (!request.IsActive)
        {
            await accountService.SetTeamLoginEnabledAsync(team.Id, false, cancellationToken);
        }

        var scopeResult = await orgScopeService.ReplaceScopesAsync(
            OrgScopeOwnerTypes.Team, team.Id.ToString(), request.Scopes, cancellationToken);

        if (!scopeResult.IsSuccess)
        {
            return Result<long>.Fail(scopeResult.Errors);
        }

        return Result<long>.Success(team.Id);
    }
}
