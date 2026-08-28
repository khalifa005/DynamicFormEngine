namespace KH.Application.Permissions.Models;

/// <summary>
/// A role and the permissions it currently holds. Lives in the Permissions slice rather than a
/// <c>Roles</c> one so the namespace does not shadow <see cref="KH.Domain.Constants.Roles"/> for
/// every file in the Application assembly.
/// </summary>
public sealed class RoleDto
{
    /// <summary>The role key — what the JWT and every authorization check use.</summary>
    public string Name { get; init; } = string.Empty;

    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;

    /// <summary>
    /// True for the super admin, whose access comes from a bypass rather than from grants. Its
    /// permission set is not meaningfully editable, so the admin screen shows it read-only.
    /// </summary>
    public bool IsSystem { get; init; }

    public IReadOnlyList<string> AssignedPermissionCodes { get; init; } = [];
}
