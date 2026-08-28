using System.Globalization;
using KH.Application.Common.Interfaces;
using KH.Domain.Constants;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Org;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Fsms.Common.Org;

public sealed class OrgScopeService(
    IApplicationDbContext context,
    IUser user,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IOrgScopeService
{
    public async Task<OrgScopeSet> GetScopeAsync(
        string ownerType, string ownerId, CancellationToken cancellationToken)
    {
        var rows = await context.OrgScopes
            .AsNoTracking()
            .Where(x => x.OwnerType == ownerType && x.OwnerId == ownerId && x.IsActive)
            .Select(x => new OrgScopeAssignment
            {
                Level = x.Level,
                Code = x.Code,
                DepartmentId = x.DepartmentId
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return OrgScopeSet.Unrestricted();
        }

        var hierarchy = await OrgHierarchyCache.GetAsync(context, cache, cacheSettings.Value, cancellationToken);

        return OrgScopeSet.FromAssignments(rows, hierarchy);
    }

    public async Task<IReadOnlyDictionary<string, OrgScopeSet>> GetScopesAsync(
        string ownerType, IReadOnlyCollection<string> ownerIds, CancellationToken cancellationToken)
    {
        if (ownerIds.Count == 0)
        {
            return new Dictionary<string, OrgScopeSet>();
        }

        var rows = await context.OrgScopes
            .AsNoTracking()
            .Where(x => x.OwnerType == ownerType && x.IsActive && ownerIds.Contains(x.OwnerId))
            .Select(x => new { x.OwnerId, x.Level, x.Code, x.DepartmentId })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return new Dictionary<string, OrgScopeSet>();
        }

        var hierarchy = await OrgHierarchyCache.GetAsync(context, cache, cacheSettings.Value, cancellationToken);

        return rows
            .GroupBy(x => x.OwnerId)
            .ToDictionary(
                g => g.Key,
                g => OrgScopeSet.FromAssignments(
                    g.Select(x => new OrgScopeAssignment { Level = x.Level, Code = x.Code, DepartmentId = x.DepartmentId }),
                    hierarchy));
    }

    public Task<OrgScopeSet> GetCurrentUserScopeAsync(CancellationToken cancellationToken)
    {
        if (user.IsInRole(Roles.Administrator) || user.IsInRole(FsmsRoles.Monitor))
        {
            return Task.FromResult(OrgScopeSet.Unrestricted());
        }

        // Field-team logins inherit the crew's territory — scope rows live on the Team, not the user.
        if (user.FieldTeamId is long fieldTeamId)
        {
            return GetScopeAsync(
                OrgScopeOwnerTypes.Team,
                fieldTeamId.ToString(CultureInfo.InvariantCulture),
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(user.Id))
        {
            return Task.FromResult(OrgScopeSet.Unrestricted());
        }

        return GetScopeAsync(OrgScopeOwnerTypes.User, user.Id, cancellationToken);
    }

    public async Task<Result<bool>> ReplaceScopesAsync(
        string ownerType,
        string ownerId,
        IReadOnlyList<OrgScopeAssignment> assignments,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(assignments, cancellationToken);

        if (!validation.IsSuccess)
        {
            return validation;
        }

        var existing = await context.OrgScopes
            .Where(x => x.OwnerType == ownerType && x.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

        context.OrgScopes.RemoveRange(existing);

        foreach (var assignment in assignments)
        {
            context.OrgScopes.Add(OrgScope.Create(
                ownerType, ownerId, assignment.Level, assignment.Code, assignment.DepartmentId));
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ValidateAsync(
        IReadOnlyList<OrgScopeAssignment> assignments,
        CancellationToken cancellationToken)
    {
        if (assignments.Count == 0)
        {
            return Result<bool>.Success(true);
        }

        foreach (var assignment in assignments)
        {
            var hasLevel = !string.IsNullOrWhiteSpace(assignment.Level);
            var hasCode = !string.IsNullOrWhiteSpace(assignment.Code);

            if (hasLevel != hasCode)
            {
                return Fail("A scope must name both a level and a code, or neither.");
            }

            if (!hasLevel && assignment.DepartmentId is null)
            {
                return Fail(
                    "A scope must name a territory, a department, or both. Leave the list empty to cover everything.");
            }

            if (hasLevel && !OrgScopeLevels.IsDefined(assignment.Level))
            {
                return Fail($"Unknown scope level '{assignment.Level}'.");
            }
        }

        var hierarchy = await OrgHierarchyCache.GetAsync(context, cache, cacheSettings.Value, cancellationToken);
        var departmentIds = await KnownDepartmentIdsAsync(cancellationToken);

        foreach (var assignment in assignments)
        {
            // The level is part of the lookup key, so a hit is already proof the code names a real
            // unit at that level — there is nothing left to re-check afterwards.
            if (assignment.Level is not null && hierarchy.Find(assignment.Level, assignment.Code) is null)
            {
                return Fail($"'{assignment.Code}' does not exist at level {assignment.Level}.");
            }

            if (assignment.DepartmentId is int departmentId && !departmentIds.Contains(departmentId))
            {
                return Fail($"Department {departmentId} does not exist.");
            }
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Department ids that exist, read once per validation rather than once per row. Departments are
    /// a handful of slow-moving rows, so the whole set is cheaper to hold than a query per scope.
    /// </summary>
    private async Task<HashSet<int>> KnownDepartmentIdsAsync(CancellationToken cancellationToken)
    {
        var ids = await context.FsmsDepartments
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        return [.. ids];
    }

    private static Result<bool> Fail(string message) =>
        Result<bool>.Fail(message, ApiErrorCodes.ValidationError, httpStatusCode: 400);
}
