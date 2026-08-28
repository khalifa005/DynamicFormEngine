namespace KH.Domain.Constants;

/// <summary>
/// Permission policies used for authorization. Each policy is a discrete permission.
/// </summary>
public abstract class Policies
{
    public const string CanPurge = nameof(CanPurge);

    public const string CanManageRolePermissions = nameof(CanManageRolePermissions);

    public static readonly IReadOnlyList<string> All =
    [
        CanPurge,
        CanManageRolePermissions
    ];
}
