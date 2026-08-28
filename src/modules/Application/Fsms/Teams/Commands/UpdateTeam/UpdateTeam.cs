using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Teams;
using Shared.Core.Common;

namespace KH.Application.Fsms.Teams.Commands.UpdateTeam;

[Authorize(Policy = FsmsPolicies.ManageTeams)]
public record UpdateTeamCommand : IRequest<Result<long>>
{
    public long Id { get; init; }
    public string UserCode { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string? Mobile { get; init; }
    public string? TeamType { get; init; }
    public int? ContractorId { get; init; }
    public IReadOnlyList<OrgScopeAssignment> Scopes { get; init; } = [];
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateTeamCommandValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Team id is required.");

        RuleFor(x => x.UserCode)
            .NotEmpty().WithMessage("User code is required.")
            .MaximumLength(50).WithMessage("User code must not exceed 50 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(250).WithMessage("Name must not exceed 250 characters.");

        RuleFor(x => x.ContractorId)
            .GreaterThan(0).WithMessage("Contractor id is not valid.")
            .When(x => x.ContractorId.HasValue);

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

public sealed class UpdateTeamCommandHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService,
    IUserAccountService accountService)
    : IRequestHandler<UpdateTeamCommand, Result<long>>
{
    public async Task<Result<long>> Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await context.FieldTeams.FindAsync(new object[] { request.Id }, cancellationToken);

        if (team is null)
        {
            return Result<long>.Fail("Team not found.");
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

        team.UserCode = request.UserCode;
        team.Name = request.Name;
        team.Mobile = request.Mobile;
        team.TeamType = request.TeamType;
        team.ContractorId = request.ContractorId;
        team.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);

        var scopeResult = await orgScopeService.ReplaceScopesAsync(
            OrgScopeOwnerTypes.Team, team.Id.ToString(), request.Scopes, cancellationToken);

        if (!scopeResult.IsSuccess)
        {
            return Result<long>.Fail(scopeResult.Errors);
        }

        // Retiring the team has to reach its login as well, or the crew carries on signing in to an
        // app with no work left for them.
        var loginResult = await accountService.SetTeamLoginEnabledAsync(
            team.Id, request.IsActive, cancellationToken);

        if (!loginResult.IsSuccess)
        {
            return Result<long>.Fail(loginResult.Errors);
        }

        return Result<long>.Success(team.Id);
    }
}
