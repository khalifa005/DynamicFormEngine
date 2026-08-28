using KH.Domain.Constants.Fsms;

namespace KH.Domain.Constants;

/// <summary>
/// Human-readable labels for the canonical roles, in both languages. The role *key* is what the
/// database, the JWT and every <c>[Authorize(Roles = ...)]</c> use; these are only what an operator
/// sees. Kept apart so a label can be reworded without touching authorization.
/// </summary>
public abstract class RoleDisplayNames
{
    private static readonly IReadOnlyDictionary<string, (string NameEn, string NameAr)> Names =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            [Roles.Administrator] = ("Super Admin", "مدير النظام"),
            [FsmsRoles.SupportOperations] = ("Support Operations", "فريق الدعم والتشغيل"),
            [FsmsRoles.Dispatcher] = ("Back Office", "المكتب الخلفي"),
            [FsmsRoles.Reviewer] = ("Reviewer", "المراجع"),
            [FsmsRoles.Monitor] = ("Monitor", "المتابع"),
            [FsmsRoles.FieldTeam] = ("Field Team", "الفريق الميداني")
        };

    /// <summary>Every role the application ships with, super admin first.</summary>
    public static readonly IReadOnlyList<string> AllRoles =
    [
        Roles.Administrator,
        FsmsRoles.SupportOperations,
        FsmsRoles.Dispatcher,
        FsmsRoles.Reviewer,
        FsmsRoles.Monitor,
        FsmsRoles.FieldTeam
    ];

    /// <summary>Falls back to the role key so an unknown role still renders as something.</summary>
    public static (string NameEn, string NameAr) For(string roleName) =>
        Names.TryGetValue(roleName, out var display) ? display : (roleName, roleName);

    public static bool IsCanonical(string? roleName) =>
        roleName is not null && Names.ContainsKey(roleName);

    /// <summary>
    /// Where the role sits when the six are listed for a human — super admin first, field crew last.
    /// Anything unrecognised sorts to the end rather than pretending to a position.
    /// </summary>
    public static int SortOrder(string roleName)
    {
        for (var index = 0; index < AllRoles.Count; index++)
        {
            if (string.Equals(AllRoles[index], roleName, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return int.MaxValue;
    }
}
