using System.Globalization;
using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Teams.Queries.GetEligibleTeamsForSurvey;

/// <summary>
/// The crews that may actually be given one particular survey: active, inside the caller's own
/// territory, and scoped to the survey's location and department.
///
/// Narrower than the teams grid on purpose. The grid answers "which crews may I see", which is an
/// overlap question; allocation asks "which crews cover this work", which is containment — and
/// offering a crew the allocation endpoint will refuse is a click that can only end in a 400.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewSurveys)]
public record GetEligibleTeamsForSurveyQuery : IRequest<Result<IReadOnlyList<EligibleTeamDto>>>
{
    public long SurveyId { get; init; }
}

public sealed class GetEligibleTeamsForSurveyQueryValidator : AbstractValidator<GetEligibleTeamsForSurveyQuery>
{
    public GetEligibleTeamsForSurveyQueryValidator()
    {
        RuleFor(x => x.SurveyId)
            .GreaterThan(0).WithMessage("Survey id is required.");
    }
}

public sealed class EligibleTeamDto
{
    public long TeamId { get; init; }
    public string UserCode { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string? Mobile { get; init; }
    public string? TeamType { get; init; }

    /// <summary>
    /// Surveys the crew is currently holding, so the picker can show how loaded each one already is
    /// before another survey is added to it.
    /// </summary>
    public int ActiveAssignmentCount { get; init; }
}

public sealed class GetEligibleTeamsForSurveyQueryHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService)
    : IRequestHandler<GetEligibleTeamsForSurveyQuery, Result<IReadOnlyList<EligibleTeamDto>>>
{
    public async Task<Result<IReadOnlyList<EligibleTeamDto>>> Handle(
        GetEligibleTeamsForSurveyQuery request,
        CancellationToken cancellationToken)
    {
        var survey = await context.Surveys
            .AsNoTracking()
            .Where(x => x.Id == request.SurveyId)
            .Select(x => new
            {
                x.CbuCode,
                x.BranchCode,
                x.OperationAreaCode,
                x.DepartmentId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (survey is null)
        {
            return Result<IReadOnlyList<EligibleTeamDto>>.Fail(
                "Survey not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);

        if (!callerScope.Covers(survey.CbuCode, survey.BranchCode, survey.OperationAreaCode, survey.DepartmentId))
        {
            return Result<IReadOnlyList<EligibleTeamDto>>.Fail(
                "This survey is outside your territory.", ApiErrorCodes.UnauthorizedAccess, httpStatusCode: 403);
        }

        var teams = await context.FieldTeams
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.UserCode)
            .Select(x => new
            {
                x.Id,
                x.UserCode,
                x.Name,
                x.Mobile,
                x.TeamType
            })
            .ToListAsync(cancellationToken);

        if (teams.Count == 0)
        {
            return Result<IReadOnlyList<EligibleTeamDto>>.Success([]);
        }

        var scopes = await orgScopeService.GetScopesAsync(
            OrgScopeOwnerTypes.Team,
            teams.Select(x => x.Id.ToString(CultureInfo.InvariantCulture)).ToList(),
            cancellationToken);

        var eligibleTeams = teams
            .Where(team =>
            {
                // A crew with no scope rows covers everything — OrgScopeSet's own "no rows =
                // unrestricted" rule, so coverage can be rolled out without blanking the picker.
                var scope = scopes.GetValueOrDefault(
                    team.Id.ToString(CultureInfo.InvariantCulture),
                    OrgScopeSet.Unrestricted());

                return callerScope.Overlaps(scope)
                    && scope.Covers(survey.CbuCode, survey.BranchCode, survey.OperationAreaCode, survey.DepartmentId);
            })
            .ToList();

        if (eligibleTeams.Count == 0)
        {
            return Result<IReadOnlyList<EligibleTeamDto>>.Success([]);
        }

        // Counted only for the crews that survived the scope filter — the picker is the only thing
        // that shows the number, so there is no point costing a count for a crew it will not list.
        var eligibleIds = eligibleTeams.Select(x => x.Id).ToList();

        var workload = await context.SurveyAssignments
            .AsNoTracking()
            .Where(x => x.IsActive && eligibleIds.Contains(x.FieldTeamId))
            .GroupBy(x => x.FieldTeamId)
            .Select(g => new { FieldTeamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FieldTeamId, x => x.Count, cancellationToken);

        var result = eligibleTeams
            .Select(team => new EligibleTeamDto
            {
                TeamId = team.Id,
                UserCode = team.UserCode,
                Name = team.Name,
                Mobile = team.Mobile,
                TeamType = team.TeamType,
                ActiveAssignmentCount = workload.GetValueOrDefault(team.Id)
            })
            .ToList();

        return Result<IReadOnlyList<EligibleTeamDto>>.Success(result);
    }
}
