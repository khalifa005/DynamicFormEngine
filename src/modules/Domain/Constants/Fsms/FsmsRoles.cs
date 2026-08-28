namespace KH.Domain.Constants.Fsms;

/// <summary>
/// Application roles specific to the Field Survey Management System.
///
/// These five plus <see cref="Roles.Administrator"/> are the complete role set — nothing else is
/// seeded, and <c>RoleSeedData</c> prunes any role outside it. Grants per role live in
/// <c>RolePermissionSeedData</c>; the matrix is editable afterwards from the admin screen.
/// </summary>
public abstract class FsmsRoles
{
    /// <summary>
    /// Support &amp; operations: owns survey templates, the reference lookups and the field-team
    /// register. Previously named <c>TemplateDesigner</c> — renamed in place by the seeder so the
    /// role Id (and every user/permission link hanging off it) survives.
    /// </summary>
    public const string SupportOperations = nameof(SupportOperations);

    /// <summary>
    /// The back office. Reads survey data, allocates surveys to field teams and reviews what comes
    /// back. Kept under the name <c>Dispatcher</c> because that is what the seeded database already
    /// calls it; "Back Office" is its display label (see <see cref="RoleDisplayNames"/>).
    /// </summary>
    public const string Dispatcher = nameof(Dispatcher);

    /// <summary>A field crew: receives allocated surveys and fills them in.</summary>
    public const string FieldTeam = nameof(FieldTeam);

    /// <summary>
    /// Reviews submitted surveys — completes them, or returns them for rework to the same crew or a
    /// different one, with a reason code and a note.
    /// </summary>
    public const string Reviewer = nameof(Reviewer);

    /// <summary>Read-only oversight across all application data. Holds no write permission.</summary>
    public const string Monitor = nameof(Monitor);

    public static readonly IReadOnlyList<string> All =
    [
        SupportOperations,
        Dispatcher,
        FieldTeam,
        Reviewer,
        Monitor
    ];

    /// <summary>The role this one replaced; kept only so the seeder can rename it in place.</summary>
    public const string LegacyTemplateDesigner = "TemplateDesigner";

    /// <summary>
    /// Retired second admin tier. It never bypassed permission checks (only
    /// <see cref="Roles.Administrator"/> does), so it was a trap rather than a role. The seeder
    /// moves its members to <see cref="Roles.Administrator"/> and deletes it.
    /// </summary>
    public const string LegacyAdmin = "Admin";
}
