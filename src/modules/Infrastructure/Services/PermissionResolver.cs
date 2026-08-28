using KH.Application.Common.Interfaces;
using KH.Infrastructure.Data;
using KH.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace KH.Infrastructure.Services;

public sealed class PermissionResolver(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IMemoryCache cache) : IPermissionResolver
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    public async Task<IReadOnlyList<string>> GetPermissionCodesForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = UserCacheKey(userId);

        return await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            var user = await userManager.FindByIdAsync(userId);

            if (user is null)
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return (IReadOnlyList<string>)[];
            }

            var roleNames = await userManager.GetRolesAsync(user);

            if (roleNames.Count == 0)
            {
                return (IReadOnlyList<string>)[];
            }

            var roleIds = await context.Roles
                .AsNoTracking()
                .Where(role => role.Name != null && roleNames.Contains(role.Name))
                .Select(role => role.Id)
                .ToListAsync(cancellationToken);

            if (roleIds.Count == 0)
            {
                return (IReadOnlyList<string>)[];
            }

            var permissions = await context.RolePermissions
                .AsNoTracking()
                .Where(x => roleIds.Contains(x.RoleId))
                .Where(x => x.Permission.IsActive)
                .Select(x => x.Permission.Code)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);

            return permissions;
        }) ?? [];
    }

    public async Task<IReadOnlyList<string>> GetPermissionCodesForRoleAsync(
        string roleId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = RoleCacheKey(roleId);

        return await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            return await context.RolePermissions
                .AsNoTracking()
                .Where(x => x.RoleId == roleId)
                .Where(x => x.Permission.IsActive)
                .Select(x => x.Permission.Code)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);
        }) ?? [];
    }

    public void InvalidateUser(string userId) =>
        cache.Remove(UserCacheKey(userId));

    public async Task InvalidateRoleAsync(string roleId, CancellationToken cancellationToken = default)
    {
        cache.Remove(RoleCacheKey(roleId));

        var userIds = await context.UserRoles
            .AsNoTracking()
            .Where(x => x.RoleId == roleId)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        foreach (var userId in userIds)
        {
            cache.Remove(UserCacheKey(userId));
        }
    }

    private static string UserCacheKey(string userId) => $"permissions:user:{userId}";

    private static string RoleCacheKey(string roleId) => $"permissions:role:{roleId}";
}
