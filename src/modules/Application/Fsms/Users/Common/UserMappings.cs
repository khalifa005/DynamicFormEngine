using KH.Application.Common.Interfaces;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Users.Models;
using KH.Domain.Constants.Fsms;

namespace KH.Application.Fsms.Users.Common;

/// <summary>Shared shaping between the <see cref="IUserAccountService"/> account shape and the FSMS DTOs.</summary>
internal static class UserMappings
{
    public static UserListItemDto ToListItemDto(this UserAccount account) => new()
    {
        Id = account.Id,
        UserName = account.UserName,
        Email = account.Email,
        PhoneNumber = account.PhoneNumber,
        FieldTeamId = account.FieldTeamId,
        IsEnabled = account.IsEnabled,
        Roles = account.Roles
    };

    public static UserDetailDto ToDetailDto(
        this UserAccount account,
        IReadOnlyList<OrgScopeAssignment> scopes) => new()
    {
        Id = account.Id,
        UserName = account.UserName,
        Email = account.Email,
        PhoneNumber = account.PhoneNumber,
        FieldTeamId = account.FieldTeamId,
        IsEnabled = account.IsEnabled,
        Roles = account.Roles,
        Scopes = scopes
    };

    /// <summary>
    /// An account's coverage. Each row carries its own department, so there is no separate
    /// department list to load alongside this one.
    /// </summary>
    public static async Task<IReadOnlyList<OrgScopeAssignment>> LoadScopesAsync(
        this IApplicationDbContext context,
        string userId,
        CancellationToken cancellationToken)
    {
        return await context.OrgScopes
            .AsNoTracking()
            .Where(x => x.OwnerType == OrgScopeOwnerTypes.User && x.OwnerId == userId && x.IsActive)
            .Select(x => new OrgScopeAssignment
            {
                Level = x.Level,
                Code = x.Code,
                DepartmentId = x.DepartmentId
            })
            .ToListAsync(cancellationToken);
    }
}
