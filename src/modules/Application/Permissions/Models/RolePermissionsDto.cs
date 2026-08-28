namespace KH.Application.Permissions.Models;

public sealed class RolePermissionsDto
{
    public string RoleName { get; init; } = default!;

    public IReadOnlyList<string> AssignedPermissionCodes { get; init; } = [];
}
