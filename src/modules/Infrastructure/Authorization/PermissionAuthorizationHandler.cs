using System.Security.Claims;
using KH.Application.Common.Interfaces;
using KH.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace KH.Infrastructure.Authorization;

public sealed class PermissionAuthorizationHandler(IPermissionResolver permissionResolver) : IAuthorizationHandler
{
    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        var pending = context.PendingRequirements.ToList();
        var hasPermissionRequirements = pending.Any(r =>
            r is PermissionRequirement or AnyPermissionRequirement);

        if (!hasPermissionRequirements)
        {
            return;
        }

        if (IsAdministrator(context.User))
        {
            foreach (var requirement in pending)
            {
                if (requirement is PermissionRequirement or AnyPermissionRequirement)
                {
                    context.Succeed(requirement);
                }
            }

            return;
        }

        var resolvedPermissions = await ResolvePermissionsAsync(context.User);
        var resolvedSet = resolvedPermissions is { Count: > 0 }
            ? new HashSet<string>(resolvedPermissions, StringComparer.OrdinalIgnoreCase)
            : null;

        foreach (var requirement in pending)
        {
            switch (requirement)
            {
                case PermissionRequirement permissionRequirement:
                    if (HasPermission(context.User, permissionRequirement.PermissionCode, resolvedSet))
                    {
                        context.Succeed(requirement);
                    }

                    break;

                case AnyPermissionRequirement anyPermissionRequirement:
                    if (anyPermissionRequirement.PermissionCodes.Any(code =>
                            HasPermission(context.User, code, resolvedSet)))
                    {
                        context.Succeed(requirement);
                    }

                    break;
            }
        }
    }

    private async Task<IReadOnlyList<string>?> ResolvePermissionsAsync(ClaimsPrincipal user)
    {
        if (user.HasClaim(c => c.Type == PermissionClaimTypes.Permission))
        {
            return null;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        return await permissionResolver.GetPermissionCodesForUserAsync(userId);
    }

    private static bool HasPermission(
        ClaimsPrincipal user,
        string permissionCode,
        HashSet<string>? resolvedPermissions)
    {
        if (user.HasClaim(PermissionClaimTypes.Permission, permissionCode))
        {
            return true;
        }

        return resolvedPermissions?.Contains(permissionCode) ?? false;
    }

    private static bool IsAdministrator(ClaimsPrincipal user) =>
        user.IsInRole(Roles.Administrator);
}
