namespace KH.Domain.Constants;

public static class PermissionPolicies
{
    public const string AnyPrefix = "Permissions.Any:";

    public static string Any(params string[] permissionCodes) =>
        AnyPrefix + string.Join(',', permissionCodes);
}
