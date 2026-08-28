using KH.Domain.Constants;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KH.Infrastructure.Data;

/// <summary>
/// The default permission grant per role — what each role can do the moment it is created, so a
/// fresh deployment is usable without anyone opening the admin screen first.
///
/// Applied only to a role that currently holds no grants at all. Once a role has been given its
/// defaults, an operator editing the matrix owns it: re-applying on every start would quietly undo
/// their work on the next restart.
/// </summary>
internal static class RolePermissionSeedData
{
    /// <summary>
    /// <see cref="Roles.Administrator"/> is absent on purpose — it bypasses permission checks
    /// outright and separately receives every active permission via
    /// <see cref="PermissionSeedData.AssignAllPermissionsToRoleAsync"/>, so listing it here would
    /// mean maintaining the same truth twice.
    /// </summary>
    internal static readonly (string Role, string[] Permissions)[] Matrix =
    [
        (FsmsRoles.SupportOperations,
        [
            FsmsPolicies.ViewDashboard,
            FsmsPolicies.ViewReports,
            FsmsPolicies.ViewTemplates,
            FsmsPolicies.ManageTemplates,
            FsmsPolicies.ManageTeams,
            FsmsPolicies.ManageLookups,
            FsmsPolicies.ManageUsers,
            FsmsPolicies.ViewSurveys
        ]),

        // Back office: sees the data, hands surveys out, and reviews what comes back.
        (FsmsRoles.Dispatcher,
        [
            FsmsPolicies.ViewDashboard,
            FsmsPolicies.ViewReports,
            FsmsPolicies.ViewTemplates,
            FsmsPolicies.ViewSurveys,
            FsmsPolicies.ManageAssignments,
            FsmsPolicies.CreateSurveys,
            FsmsPolicies.SubmitSurveys,
            FsmsPolicies.ReviewSurveys
        ]),

        (FsmsRoles.Reviewer,
        [
            FsmsPolicies.ViewDashboard,
            FsmsPolicies.ViewReports,
            FsmsPolicies.ViewTemplates,
            FsmsPolicies.ViewSurveys,
            FsmsPolicies.ReviewSurveys
        ]),

        // Read-only by construction: not one write permission in the list.
        (FsmsRoles.Monitor,
        [
            FsmsPolicies.ViewDashboard,
            FsmsPolicies.ViewReports,
            FsmsPolicies.ViewTemplates,
            FsmsPolicies.ViewSurveys
        ]),

        // Crews fill forms and may raise new surveys — not allocate to other crews.
        (FsmsRoles.FieldTeam,
        [
            FsmsPolicies.ViewTemplates,
            FsmsPolicies.ViewSurveys,
            FsmsPolicies.SubmitSurveys,
            FsmsPolicies.CreateSurveys
        ])
    ];

    /// <summary>
    /// Additive grants applied even when a role already has permissions. Use for deliberate
    /// product expansions of a role's default surface — never for the full matrix, which would
    /// undo operator edits on every restart.
    /// </summary>
    private static readonly (string Role, string Permission)[] AdditiveGrants =
    [
        (FsmsRoles.FieldTeam, FsmsPolicies.CreateSurveys),
        (FsmsRoles.Dispatcher, FsmsPolicies.CreateSurveys)
    ];

    /// <summary>
    /// Grants previously pushed by an additive seed that the product later withdrew. Removing only
    /// these pairs keeps any operator-edited grants on other roles untouched.
    /// </summary>
    private static readonly (string Role, string Permission)[] RevokedGrants =
    [
        (FsmsRoles.FieldTeam, FsmsPolicies.ManageAssignments)
    ];

    internal static async Task SeedAsync(
        ApplicationDbContext context,
        RoleManager<IdentityRole> roleManager,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        foreach (var (roleName, permissionCodes) in Matrix)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var role = await roleManager.FindByNameAsync(roleName);

            if (role is null)
            {
                continue;
            }

            var alreadyConfigured = await context.RolePermissions
                .AnyAsync(x => x.RoleId == role.Id, cancellationToken);

            if (alreadyConfigured)
            {
                continue;
            }

            var permissionIds = await context.Permissions
                .Where(x => x.IsActive && permissionCodes.Contains(x.Code))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            foreach (var permissionId in permissionIds)
            {
                context.RolePermissions.Add(RolePermission.Create(role.Id, permissionId));
            }

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Granted {GrantCount} default permissions to role {Role}.",
                permissionIds.Count,
                roleName);
        }

        await EnsureAdditiveGrantsAsync(context, roleManager, logger, cancellationToken);
        await RevokeWithdrawnGrantsAsync(context, roleManager, logger, cancellationToken);
    }

    /// <summary>
    /// Ensures product-expanded default grants exist on already-configured roles without touching
    /// any other permission the operator may have added or removed.
    /// </summary>
    private static async Task EnsureAdditiveGrantsAsync(
        ApplicationDbContext context,
        RoleManager<IdentityRole> roleManager,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var (roleName, permissionCode) in AdditiveGrants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            var permission = await context.Permissions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IsActive && x.Code == permissionCode, cancellationToken);

            if (permission is null)
            {
                continue;
            }

            var alreadyGranted = await context.RolePermissions
                .AnyAsync(x => x.RoleId == role.Id && x.PermissionId == permission.Id, cancellationToken);

            if (alreadyGranted)
            {
                continue;
            }

            context.RolePermissions.Add(RolePermission.Create(role.Id, permission.Id));
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Additively granted {Permission} to role {Role}.",
                permissionCode,
                roleName);
        }
    }

    private static async Task RevokeWithdrawnGrantsAsync(
        ApplicationDbContext context,
        RoleManager<IdentityRole> roleManager,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var (roleName, permissionCode) in RevokedGrants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            var permission = await context.Permissions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == permissionCode, cancellationToken);

            if (permission is null)
            {
                continue;
            }

            var grants = await context.RolePermissions
                .Where(x => x.RoleId == role.Id && x.PermissionId == permission.Id)
                .ToListAsync(cancellationToken);

            if (grants.Count == 0)
            {
                continue;
            }

            context.RolePermissions.RemoveRange(grants);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Revoked withdrawn permission {Permission} from role {Role}.",
                permissionCode,
                roleName);
        }
    }
}
