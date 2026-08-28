using System.Globalization;
using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Lookups;
using KH.Application.Fsms.Common.Org;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Fsms.Surveys.Queries.GetAllocationSuggestions;

/// <summary>
/// Prepares a bulk allocation: the surveys the dispatcher picked, grouped by the location they sit
/// in, each group carrying the crews whose territory actually covers that location.
///
/// Grouping is by the full tuple rather than by branch alone because coverage is decided by all four
/// axes together — a crew covering a branch for water is still the wrong crew for waste-water work in
/// it. Two surveys only share a suggestion when they share every axis that decides it.
/// </summary>
[Authorize(Policy = FsmsPolicies.ManageAssignments)]
public record GetAllocationSuggestionsQuery : IRequest<Result<AllocationSuggestionsDto>>
{
    public IReadOnlyList<long> SurveyIds { get; init; } = [];
}

public sealed class GetAllocationSuggestionsQueryValidator : AbstractValidator<GetAllocationSuggestionsQuery>
{
    /// <summary>The suggestion read is in-memory once loaded, so the cap is on what one call may pull.</summary>
    public const int MaxSurveyIds = 500;

    public GetAllocationSuggestionsQueryValidator()
    {
        RuleFor(x => x.SurveyIds)
            .NotEmpty().WithMessage("At least one survey must be selected.")
            .Must(ids => ids.Count <= MaxSurveyIds)
            .WithMessage($"No more than {MaxSurveyIds} surveys can be allocated at once.");

        RuleForEach(x => x.SurveyIds)
            .GreaterThan(0).WithMessage("Survey id is required.");
    }
}

/// <summary>One survey inside a group — enough to name it in the dialog, no more.</summary>
public sealed class AllocationGroupSurveyDto
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;
    public string Status { get; init; } = default!;
    public long? AllocatedFieldTeamId { get; init; }
    public string? AllocatedFieldTeamName { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? CompletionDueDate { get; init; }
    public int? TeamFillSlaHours { get; init; }
    public int? CompletionSlaHours { get; init; }
}

/// <summary>A crew the dispatcher may pick for a group.</summary>
public sealed class AllocationTeamOptionDto
{
    public long TeamId { get; init; }
    public string UserCode { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string? TeamType { get; init; }

    /// <summary>
    /// Surveys the crew is currently holding. Shown beside the name so a dispatcher spreading a
    /// batch can see which crews are already loaded before piling more on them.
    /// </summary>
    public int ActiveAssignmentCount { get; init; }
}

/// <summary>
/// The surveys that share one location tuple, and the crews that can take them. A group whose
/// <see cref="CandidateTeams"/> is empty has no crew scoped to it — the dialog says so rather than
/// silently dropping the surveys.
/// </summary>
public sealed class AllocationGroupDto
{
    public string GroupKey { get; init; } = default!;
    public string? CbuCode { get; init; }
    public string? CbuNameEn { get; init; }
    public string? CbuNameAr { get; init; }
    public string? BranchCode { get; init; }
    public string? BranchNameEn { get; init; }
    public string? BranchNameAr { get; init; }
    public string? OperationAreaCode { get; init; }
    public string? OperationAreaNameEn { get; init; }
    public string? OperationAreaNameAr { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentNameEn { get; init; }
    public string? DepartmentNameAr { get; init; }
    public int SurveyCount { get; init; }
    public IReadOnlyList<AllocationGroupSurveyDto> Surveys { get; init; } = [];
    public IReadOnlyList<AllocationTeamOptionDto> CandidateTeams { get; init; } = [];

    /// <summary>The crew the dialog preselects. Null when nothing covers the group.</summary>
    public long? DefaultFieldTeamId { get; init; }
}

public sealed class AllocationSuggestionsDto
{
    public IReadOnlyList<AllocationGroupDto> Groups { get; init; } = [];

    /// <summary>
    /// Requested surveys left out — outside the caller's territory, already filled, or in a status
    /// that no longer accepts an allocation. Reported so the dialog can say what it dropped.
    /// </summary>
    public IReadOnlyList<long> IgnoredSurveyIds { get; init; } = [];
}

public sealed class GetAllocationSuggestionsQueryHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetAllocationSuggestionsQuery, Result<AllocationSuggestionsDto>>
{
    /// <summary>The statuses an allocation may still be written against — mirrors <c>Survey.Assign</c>.</summary>
    private static readonly string[] AllocatableStatuses =
    [
        SurveyStatuses.Created,
        SurveyStatuses.Assigned,
        SurveyStatuses.InProgress
    ];

    public async Task<Result<AllocationSuggestionsDto>> Handle(
        GetAllocationSuggestionsQuery request,
        CancellationToken cancellationToken)
    {
        var requestedIds = request.SurveyIds.Distinct().ToList();

        var rows = await context.Surveys
            .AsNoTracking()
            .Where(x => requestedIds.Contains(x.Id))
            .Select(x => new AllocationSurveyRow
            {
                SurveyId = x.Id,
                SurveyCode = x.SurveyCode,
                Status = x.Status,
                SubmissionCount = x.SubmissionCount,
                CbuCode = x.CbuCode,
                BranchCode = x.BranchCode,
                OperationAreaCode = x.OperationAreaCode,
                DepartmentId = x.DepartmentId,
                DueDate = x.DueDate,
                CompletionDueDate = x.CompletionDueDate,
                TeamFillSlaHours = x.TeamFillSlaHours,
                CompletionSlaHours = x.CompletionSlaHours,
                AllocatedFieldTeamId = x.Assignments
                    .Where(a => a.IsActive)
                    .OrderByDescending(a => a.AssignedDate)
                    .Select(a => (long?)a.FieldTeamId)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // Defence in depth: the worklist the ids came from is already scope-filtered, but a request
        // is not obliged to have come from it.
        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);

        var eligible = rows
            .Where(x =>
                x.SubmissionCount == 0
                && AllocatableStatuses.Contains(x.Status)
                && callerScope.Covers(x.CbuCode, x.BranchCode, x.OperationAreaCode, x.DepartmentId))
            .ToList();

        var eligibleIds = eligible.Select(x => x.SurveyId).ToHashSet();
        var ignoredSurveyIds = requestedIds.Where(id => !eligibleIds.Contains(id)).ToList();

        if (eligible.Count == 0)
        {
            return Result<AllocationSuggestionsDto>.Success(new AllocationSuggestionsDto
            {
                Groups = [],
                IgnoredSurveyIds = ignoredSurveyIds
            });
        }

        var teams = await LoadTeamsInCallerScopeAsync(callerScope, cancellationToken);

        // Read separately from the candidate list: the crew a survey is currently with may be
        // inactive or outside the caller's territory, and it still has to be nameable.
        var teamNames = await LoadAllocatedTeamNamesAsync(eligible, cancellationToken);

        var cbuNames = await LookupNameCache.GetCbuNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var branchNames = await LookupNameCache.GetBranchNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var areaNames = await LookupNameCache.GetOperationAreaNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var departmentNames = await LookupNameCache.GetDepartmentNamesAsync(context, cache, cacheSettings.Value, cancellationToken);

        var groups = eligible
            .GroupBy(x => new AllocationGroupKey(x.CbuCode, x.BranchCode, x.OperationAreaCode, x.DepartmentId))
            .Select(group =>
            {
                var key = group.Key;

                var candidates = teams
                    .Where(x => x.Scope.Covers(key.CbuCode, key.BranchCode, key.OperationAreaCode, key.DepartmentId))
                    .Select(x => x.Team)
                    .ToList();

                var cbu = cbuNames.FindCbu(key.CbuCode);
                var branch = branchNames.FindBranch(key.BranchCode);
                var area = areaNames.FindOperationArea(key.OperationAreaCode);
                var department = departmentNames.FindDepartment(key.DepartmentId);

                return new AllocationGroupDto
                {
                    GroupKey = key.ToKeyString(),
                    CbuCode = key.CbuCode,
                    CbuNameEn = cbu?.NameEn,
                    CbuNameAr = cbu?.NameAr,
                    BranchCode = key.BranchCode,
                    BranchNameEn = branch?.NameEn,
                    BranchNameAr = branch?.NameAr,
                    OperationAreaCode = key.OperationAreaCode,
                    OperationAreaNameEn = area?.NameEn,
                    OperationAreaNameAr = area?.NameAr,
                    DepartmentId = key.DepartmentId,
                    DepartmentNameEn = department?.NameEn,
                    DepartmentNameAr = department?.NameAr,
                    SurveyCount = group.Count(),
                    Surveys = group
                        .OrderBy(x => x.SurveyCode, StringComparer.Ordinal)
                        .Select(x => new AllocationGroupSurveyDto
                        {
                            SurveyId = x.SurveyId,
                            SurveyCode = x.SurveyCode,
                            Status = x.Status,
                            AllocatedFieldTeamId = x.AllocatedFieldTeamId,
                            AllocatedFieldTeamName = x.AllocatedFieldTeamId is long teamId
                                ? teamNames.GetValueOrDefault(teamId)
                                : null,
                            DueDate = x.DueDate,
                            CompletionDueDate = x.CompletionDueDate,
                            TeamFillSlaHours = x.TeamFillSlaHours,
                            CompletionSlaHours = x.CompletionSlaHours
                        })
                        .ToList(),
                    CandidateTeams = candidates,
                    // Teams are already ordered by user code, so "the first candidate" is stable
                    // between calls rather than whatever order the database happened to return.
                    DefaultFieldTeamId = candidates.Count > 0 ? candidates[0].TeamId : null
                };
            })
            .OrderBy(x => x.GroupKey, StringComparer.Ordinal)
            .ToList();

        return Result<AllocationSuggestionsDto>.Success(new AllocationSuggestionsDto
        {
            Groups = groups,
            IgnoredSurveyIds = ignoredSurveyIds
        });
    }

    /// <summary>
    /// The active crews the caller may hand work to, each with its expanded territory. Scopes are
    /// read in one batch rather than per team — the same shape the teams grid uses.
    /// </summary>
    private async Task<List<TeamWithScope>> LoadTeamsInCallerScopeAsync(
        OrgScopeSet callerScope,
        CancellationToken cancellationToken)
    {
        var teams = await context.FieldTeams
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.UserCode)
            .Select(x => new
            {
                x.Id,
                x.UserCode,
                x.Name,
                x.TeamType
            })
            .ToListAsync(cancellationToken);

        if (teams.Count == 0)
        {
            return [];
        }

        var teamIds = teams.Select(x => x.Id).ToList();

        var scopes = await orgScopeService.GetScopesAsync(
            OrgScopeOwnerTypes.Team,
            teamIds.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList(),
            cancellationToken);

        var workload = await ActiveAssignmentCountsAsync(teamIds, cancellationToken);

        return teams
            .Select(team =>
            {
                // A crew with no scope rows covers everything, matching OrgScopeSet's own semantics:
                // coverage is rolled out gradually and an unscoped crew must not go invisible.
                var scope = scopes.GetValueOrDefault(
                    team.Id.ToString(CultureInfo.InvariantCulture),
                    OrgScopeSet.Unrestricted());

                var option = new AllocationTeamOptionDto
                {
                    TeamId = team.Id,
                    UserCode = team.UserCode,
                    Name = team.Name,
                    TeamType = team.TeamType,
                    ActiveAssignmentCount = workload.GetValueOrDefault(team.Id)
                };

                return new TeamWithScope(option, scope);
            })
            .Where(x => callerScope.Overlaps(x.Scope))
            .ToList();
    }

    /// <summary>
    /// How many surveys each crew is currently holding, keyed by team id. One grouped read for the
    /// whole picker rather than a correlated count per team.
    /// </summary>
    private async Task<Dictionary<long, int>> ActiveAssignmentCountsAsync(
        List<long> teamIds,
        CancellationToken cancellationToken) =>
        await context.SurveyAssignments
            .AsNoTracking()
            .Where(x => x.IsActive && teamIds.Contains(x.FieldTeamId))
            .GroupBy(x => x.FieldTeamId)
            .Select(g => new { FieldTeamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FieldTeamId, x => x.Count, cancellationToken);

    /// <summary>Names of the crews the selected surveys currently sit with, keyed by team id.</summary>
    private async Task<Dictionary<long, string>> LoadAllocatedTeamNamesAsync(
        List<AllocationSurveyRow> rows,
        CancellationToken cancellationToken)
    {
        var teamIds = rows
            .Where(x => x.AllocatedFieldTeamId.HasValue)
            .Select(x => x.AllocatedFieldTeamId!.Value)
            .Distinct()
            .ToList();

        if (teamIds.Count == 0)
        {
            return [];
        }

        return await context.FieldTeams
            .AsNoTracking()
            .Where(x => teamIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    }
}

internal sealed record TeamWithScope(AllocationTeamOptionDto Team, OrgScopeSet Scope);

/// <summary>
/// The four axes coverage is decided on. A record so grouping compares by value; codes are compared
/// case-insensitively because the database collation does.
/// </summary>
internal sealed record AllocationGroupKey(string? CbuCode, string? BranchCode, string? OperationAreaCode, int? DepartmentId)
{
    public bool Equals(AllocationGroupKey? other) =>
        other is not null
        && string.Equals(CbuCode, other.CbuCode, StringComparison.OrdinalIgnoreCase)
        && string.Equals(BranchCode, other.BranchCode, StringComparison.OrdinalIgnoreCase)
        && string.Equals(OperationAreaCode, other.OperationAreaCode, StringComparison.OrdinalIgnoreCase)
        && DepartmentId == other.DepartmentId;

    public override int GetHashCode() => HashCode.Combine(
        CbuCode?.ToUpperInvariant(),
        BranchCode?.ToUpperInvariant(),
        OperationAreaCode?.ToUpperInvariant(),
        DepartmentId);

    /// <summary>A stable client-side identity for the group — the dialog keys its form rows on it.</summary>
    public string ToKeyString() =>
        string.Join('|', CbuCode ?? "", BranchCode ?? "", OperationAreaCode ?? "", DepartmentId?.ToString(CultureInfo.InvariantCulture) ?? "");
}

/// <summary>
/// The survey columns the grouping needs. Internal rather than nested and private: EF materializes
/// a projection through generated accessors.
/// </summary>
internal sealed class AllocationSurveyRow
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;
    public string Status { get; init; } = default!;
    public int SubmissionCount { get; init; }
    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public int? DepartmentId { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? CompletionDueDate { get; init; }
    public int? TeamFillSlaHours { get; init; }
    public int? CompletionSlaHours { get; init; }
    public long? AllocatedFieldTeamId { get; init; }
}
