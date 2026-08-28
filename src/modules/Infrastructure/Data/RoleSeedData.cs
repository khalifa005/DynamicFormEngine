using KH.Domain.Constants;
using KH.Domain.Constants.Fsms;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KH.Infrastructure.Data;

/// <summary>
/// Brings <c>AspNetRoles</c> in line with <see cref="RoleDisplayNames.AllRoles"/> — the six roles the
/// application actually has — and nothing else. Idempotent: safe to run on every start, on a fresh
/// database or on one carrying the older role set.
/// </summary>
internal static class RoleSeedData
{
    internal static async Task SeedAsync(
        ApplicationDbContext context,
        UserManager<Identity.ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await RenameLegacyRolesAsync(roleManager, logger, cancellationToken);
        await EnsureCanonicalRolesAsync(roleManager, logger, cancellationToken);
        await RetireLegacyAdminAsync(context, userManager, roleManager, logger, cancellationToken);
        await PruneNonCanonicalRolesAsync(context, roleManager, logger, cancellationToken);
    }

    /// <summary>
    /// Renames rather than recreates, so the role Id is preserved and every <c>AspNetUserRoles</c>
    /// and <c>SM_ROLE_PERMISSION</c> row hanging off it follows automatically. Dropping and
    /// recreating would silently unassign every user.
    /// </summary>
    private static async Task RenameLegacyRolesAsync(
        RoleManager<IdentityRole> roleManager,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        (string From, string To)[] renames =
        [
            (FsmsRoles.LegacyTemplateDesigner, FsmsRoles.SupportOperations)
        ];

        foreach (var (from, to) in renames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var legacy = await roleManager.FindByNameAsync(from);

            if (legacy is null)
            {
                continue;
            }

            // If the target somehow already exists the rename would collide; leave both alone and
            // let the prune step deal with the legacy one.
            if (await roleManager.RoleExistsAsync(to))
            {
                continue;
            }

            var result = await roleManager.SetRoleNameAsync(legacy, to);

            if (result.Succeeded)
            {
                result = await roleManager.UpdateAsync(legacy);
            }

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to rename role '{from}' to '{to}': {Describe(result)}");
            }

            logger.LogInformation("Renamed role {FromRole} to {ToRole} (Id {RoleId} preserved).", from, to, legacy.Id);
        }
    }

    private static async Task EnsureCanonicalRolesAsync(
        RoleManager<IdentityRole> roleManager,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var roleName in RoleDisplayNames.AllRoles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));

            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create role '{roleName}': {Describe(result)}");
            }

            logger.LogInformation("Created role {Role}.", roleName);
        }
    }

    /// <summary>
    /// The old <c>Admin</c> role looked like a super admin but bypassed nothing — only
    /// <see cref="Roles.Administrator"/> does. Its members are moved there before it is deleted, so
    /// nobody silently loses access.
    /// </summary>
    private static async Task RetireLegacyAdminAsync(
        ApplicationDbContext context,
        UserManager<Identity.ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var legacyAdmin = await roleManager.FindByNameAsync(FsmsRoles.LegacyAdmin);

        if (legacyAdmin is null)
        {
            return;
        }

        var members = await userManager.GetUsersInRoleAsync(FsmsRoles.LegacyAdmin);

        foreach (var member in members)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await userManager.IsInRoleAsync(member, Roles.Administrator))
            {
                await userManager.AddToRoleAsync(member, Roles.Administrator);
            }

            await userManager.RemoveFromRoleAsync(member, FsmsRoles.LegacyAdmin);

            logger.LogInformation(
                "Moved user {UserName} from the retired {LegacyRole} role to {Role}.",
                member.UserName,
                FsmsRoles.LegacyAdmin,
                Roles.Administrator);
        }

        await DeleteRoleAsync(context, roleManager, legacyAdmin, logger, cancellationToken);
    }

    /// <summary>
    /// Enforces "only these roles exist". Anything outside the canonical set goes, along with its
    /// grants.
    /// </summary>
    private static async Task PruneNonCanonicalRolesAsync(
        ApplicationDbContext context,
        RoleManager<IdentityRole> roleManager,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var strays = await roleManager.Roles
            .Where(r => r.Name != null && !RoleDisplayNames.AllRoles.Contains(r.Name))
            .ToListAsync(cancellationToken);

        foreach (var stray in strays)
        {
            await DeleteRoleAsync(context, roleManager, stray, logger, cancellationToken);
        }
    }

    /// <summary>
    /// <c>SM_ROLE_PERMISSION.RoleId</c> is a loose string with no foreign key to <c>AspNetRoles</c>,
    /// so deleting a role does not cascade to its grants. They have to go first, or they linger as
    /// orphans that the role-permission admin screen would later show against a role that no longer
    /// exists.
    /// </summary>
    private static async Task DeleteRoleAsync(
        ApplicationDbContext context,
        RoleManager<IdentityRole> roleManager,
        IdentityRole role,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var orphanedGrants = await context.RolePermissions
            .Where(x => x.RoleId == role.Id)
            .ToListAsync(cancellationToken);

        if (orphanedGrants.Count > 0)
        {
            context.RolePermissions.RemoveRange(orphanedGrants);
            await context.SaveChangesAsync(cancellationToken);
        }

        var result = await roleManager.DeleteAsync(role);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to delete role '{role.Name}': {Describe(result)}");
        }

        logger.LogInformation(
            "Deleted non-canonical role {Role} and {GrantCount} of its permission grants.",
            role.Name,
            orphanedGrants.Count);
    }

    private static string Describe(IdentityResult result) =>
        string.Join(", ", result.Errors.Select(e => e.Description));
}
