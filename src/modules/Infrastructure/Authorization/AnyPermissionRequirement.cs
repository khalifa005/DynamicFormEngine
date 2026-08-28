using Microsoft.AspNetCore.Authorization;

namespace KH.Infrastructure.Authorization;

public sealed class AnyPermissionRequirement : IAuthorizationRequirement
{
    public AnyPermissionRequirement(IReadOnlyList<string> permissionCodes)
    {
        PermissionCodes = permissionCodes;
    }

    public IReadOnlyList<string> PermissionCodes { get; }
}
