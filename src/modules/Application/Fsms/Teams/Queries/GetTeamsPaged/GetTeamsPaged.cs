using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Models;
using KH.Application.Fsms.Common.Org;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Org;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Fsms.Teams.Queries.GetTeamsPaged;

/// <summary>
/// Read by the teams admin grid and by the reviewer's return dialog when picking a different crew,
/// so it is gated on seeing surveys rather than on managing teams.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewSurveys)]
public record GetTeamsPagedQuery : IRequest<Result<PagedResult<TeamDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
    public string? ClusterCode { get; init; }
    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public int? ContractorId { get; init; }
}

public sealed class GetTeamsPagedQueryHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetTeamsPagedQuery, Result<PagedResult<TeamDto>>>
{
    public async Task<Result<PagedResult<TeamDto>>> Handle(
        GetTeamsPagedQuery request, CancellationToken cancellationToken)
    {
        var query = context.FieldTeams.AsNoTracking();

        if (request.IsActive.HasValue)
            query = query.Where(x => x.IsActive == request.IsActive.Value);

        if (request.ContractorId.HasValue)
            query = query.Where(x => x.ContractorId == request.ContractorId.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(x =>
                x.UserCode.Contains(term) ||
                x.Name.Contains(term) ||
                (x.Mobile != null && x.Mobile.Contains(term)));
        }

        var teams = await query
            .OrderBy(x => x.UserCode)
            .Select(x => new
            {
                x.Id,
                x.UserCode,
                x.Name,
                x.Mobile,
                x.TeamType,
                x.ContractorId,
                x.IsActive,
                x.DeviceName,
                x.DeviceUuid,
                x.AppVersion,
                x.DeviceOs,
                x.LastActiveLatitude,
                x.LastActiveLongitude,
                x.LastActiveAt
            })
            .ToListAsync(cancellationToken);

        var teamIds = teams.Select(x => x.Id).ToList();

        var scopeRows = await context.OrgScopes
            .AsNoTracking()
            .Where(x => x.OwnerType == OrgScopeOwnerTypes.Team && x.IsActive)
            .ToListAsync(cancellationToken);

        var scopesByTeamId = scopeRows
            .Where(x => long.TryParse(x.OwnerId, out var id) && teamIds.Contains(id))
            .GroupBy(x => long.Parse(x.OwnerId))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Departments are no longer a list beside the scopes — they ride on the scope rows already
        // loaded above, so the grid derives them rather than reading TEAM_DEPARTMENTS.
        var contractorsById = await context.FsmsContractors
            .AsNoTracking()
            .Select(x => new { x.Id, x.PoNumber, x.NameEn, x.NameAr })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var hierarchy = await OrgHierarchyCache.GetAsync(context, cache, cacheSettings.Value, cancellationToken);

        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);

        var visibleTeams = teams
            .Select(team =>
            {
                var rows = scopesByTeamId.GetValueOrDefault(team.Id, []);
                var scopeSet = OrgScopeSet.FromAssignments(rows.Select(ToAssignment), hierarchy);
                return (team, scopeSet, rows);
            })
            .Where(x => callerScope.Overlaps(x.scopeSet))
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.ClusterCode))
        {
            var filterSet = OrgScopeSet.FromAssignments([new OrgScopeAssignment { Level = OrgScopeLevels.Cluster, Code = request.ClusterCode }], hierarchy);
            visibleTeams = visibleTeams.Where(x => x.scopeSet.Overlaps(filterSet)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.CbuCode))
        {
            var filterSet = OrgScopeSet.FromAssignments([new OrgScopeAssignment { Level = OrgScopeLevels.Cbu, Code = request.CbuCode }], hierarchy);
            visibleTeams = visibleTeams.Where(x => x.scopeSet.Overlaps(filterSet)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.BranchCode))
        {
            var filterSet = OrgScopeSet.FromAssignments([new OrgScopeAssignment { Level = OrgScopeLevels.Branch, Code = request.BranchCode }], hierarchy);
            visibleTeams = visibleTeams.Where(x => x.scopeSet.Overlaps(filterSet)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.OperationAreaCode))
        {
            var filterSet = OrgScopeSet.FromAssignments([new OrgScopeAssignment { Level = OrgScopeLevels.OperationArea, Code = request.OperationAreaCode }], hierarchy);
            visibleTeams = visibleTeams.Where(x => x.scopeSet.Overlaps(filterSet)).ToList();
        }

        var totalCount = visibleTeams.Count;

        var items = visibleTeams
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new TeamDto
            {
                TeamId = x.team.Id,
                UserCode = x.team.UserCode,
                Name = x.team.Name,
                Mobile = x.team.Mobile,
                TeamType = x.team.TeamType,
                ContractorId = x.team.ContractorId,
                ContractorPoNumber = x.team.ContractorId is int contractorId && contractorsById.TryGetValue(contractorId, out var contractor)
                    ? contractor.PoNumber
                    : null,
                ContractorNameEn = x.team.ContractorId is int enId && contractorsById.TryGetValue(enId, out var contractorEn)
                    ? contractorEn.NameEn
                    : null,
                ContractorNameAr = x.team.ContractorId is int arId && contractorsById.TryGetValue(arId, out var contractorAr)
                    ? contractorAr.NameAr
                    : null,
                DepartmentIds = x.rows.Where(r => r.DepartmentId != null).Select(r => r.DepartmentId!.Value).Distinct().ToList(),
                Scopes = x.rows.Select(ToAssignment).ToList(),
                IsActive = x.team.IsActive,
                DeviceName = x.team.DeviceName,
                DeviceUuid = x.team.DeviceUuid,
                AppVersion = x.team.AppVersion,
                DeviceOs = x.team.DeviceOs,
                LastActiveLatitude = x.team.LastActiveLatitude,
                LastActiveLongitude = x.team.LastActiveLongitude,
                LastActiveAt = x.team.LastActiveAt
            })
            .ToList();

        return Result<PagedResult<TeamDto>>.Success(
            new PagedResult<TeamDto>(items, totalCount, request.PageNumber, request.PageSize));
    }

    private static OrgScopeAssignment ToAssignment(OrgScope row) => new()
    {
        Level = row.Level,
        Code = row.Code,
        DepartmentId = row.DepartmentId
    };
}
