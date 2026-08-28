using KH.Domain.Constants;
using KH.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KH.Infrastructure.Data;

internal static class PermissionSeedData
{
    internal static readonly (string Code, string Module, string NameEn, string NameAr)[] Definitions =
    [
        (Policies.CanPurge, "Admin", "Purge data", "حذف البيانات"),
        (Policies.CanManageRolePermissions, "Admin", "Manage role permissions", "إدارة صلاحيات الأدوار")
    ];

    internal static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        foreach (var definition in Definitions)
        {
            var exists = await context.Permissions.AnyAsync(
                x => x.Code == definition.Code,
                cancellationToken);

            if (exists)
            {
                continue;
            }

            context.Permissions.Add(new Permission(
                definition.Code,
                definition.Module,
                definition.NameEn,
                definition.NameAr));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Retires permission codes the application no longer defines. Deactivated rather than deleted:
    /// <c>SM_ROLE_PERMISSION</c> cascades on delete, so removing the row would take the historical
    /// grants with it, and an inactive permission is already invisible to the policy pipeline and to
    /// the admin screen.
    /// </summary>
    internal static async Task DeactivateRemovedAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var known = Definitions.Select(d => d.Code)
            .Concat(FsmsSeedData.PermissionDefinitions.Select(d => d.Code))
            .ToList();

        var retired = await context.Permissions
            .Where(x => x.IsActive && !known.Contains(x.Code))
            .ToListAsync(cancellationToken);

        if (retired.Count == 0)
        {
            return;
        }

        foreach (var permission in retired)
        {
            permission.IsActive = false;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    internal static async Task AssignAllPermissionsToRoleAsync(
        ApplicationDbContext context,
        string roleId,
        CancellationToken cancellationToken = default)
    {
        var permissionIds = await context.Permissions
            .Where(x => x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var permissionId in permissionIds)
        {
            var alreadyAssigned = await context.RolePermissions.AnyAsync(
                x => x.RoleId == roleId && x.PermissionId == permissionId,
                cancellationToken);

            if (alreadyAssigned)
            {
                continue;
            }

            context.RolePermissions.Add(RolePermission.Create(roleId, permissionId));
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
