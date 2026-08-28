namespace KH.Domain.Constants.Fsms;

/// <summary>
/// Permission-policy names for FSMS features. Each policy resolves through the permission-based
/// authorization pipeline (a matching permission code must be granted to the role).
///
/// Anything added here must also land in <see cref="All"/>, otherwise
/// <c>PermissionPolicyProvider</c> does not recognise the name and falls through to the default
/// provider, which denies the request.
/// </summary>
public abstract class FsmsPolicies
{
    // Template management (task-01)
    public const string ManageTemplates = nameof(ManageTemplates);
    public const string ViewTemplates = nameof(ViewTemplates);

    /// <summary>
    /// Read access to the survey worklist and its submissions. A plain permission rather than a
    /// composed policy: Monitor must be able to read surveys while holding none of the write
    /// permissions below, which an "any of the writers" policy cannot express.
    /// </summary>
    public const string ViewSurveys = nameof(ViewSurveys);

    public const string ManageAssignments = nameof(ManageAssignments);

    /// <summary>
    /// Raise a new survey only. Narrower than <see cref="ManageAssignments"/> so a FieldTeam can
    /// create work without also being able to allocate surveys to other crews or migrate versions.
    /// </summary>
    public const string CreateSurveys = nameof(CreateSurveys);

    public const string SubmitSurveys = nameof(SubmitSurveys);
    public const string ReviewSurveys = nameof(ReviewSurveys);

    /// <summary>Create and edit field teams and their schedules.</summary>
    public const string ManageTeams = nameof(ManageTeams);

    /// <summary>Edit the reference lookups — departments, the org geography, FA types, return reasons.</summary>
    public const string ManageLookups = nameof(ManageLookups);

    /// <summary>
    /// Create and edit back-office logins, their roles and the territory each may see. Separate from
    /// <see cref="ManageTeams"/> because a crew register and an account register are different
    /// things to be trusted with: managing users is what lets someone widen their own visibility.
    /// </summary>
    public const string ManageUsers = nameof(ManageUsers);

    /// <summary>See the dashboard and its KPI roll-ups.</summary>
    public const string ViewDashboard = nameof(ViewDashboard);

    /// <summary>View reporting/analytics: general statistics, survey tasks, and team performance reports, including their exports.</summary>
    public const string ViewReports = nameof(ViewReports);

    /// <summary>
    /// Import a historical data set from an external system. Kept apart from every other permission
    /// because of what one run does: it raises hundreds of surveys at once, closes most of them
    /// outright, and reads files off a server path an operator never chose. That is administrator
    /// work, so no role is granted it by default.
    /// </summary>
    public const string ImportData = nameof(ImportData);

    /// <summary>
    /// Either permission grants access. Used where the surveyor filling a form and the reviewer
    /// reading it need the same thing — streaming back an uploaded media file, for instance.
    /// Composed as a constant so it can sit on an attribute.
    /// </summary>
    public const string SubmitOrReviewSurveys =
        PermissionPolicies.AnyPrefix + SubmitSurveys + "," + ReviewSurveys;

    /// <summary>
    /// PDF export shared by the back-office worklist (<see cref="ViewSurveys"/>) and field-team /
    /// reviewer paths (<see cref="SubmitSurveys"/> / <see cref="ReviewSurveys"/>). Composed so a
    /// Monitor can export without submit rights, and a FieldTeam member can export without a
    /// dedicated view-only grant beyond what their role already carries.
    /// </summary>
    public const string ViewOrSubmitOrReviewSurveys =
        PermissionPolicies.AnyPrefix + ViewSurveys + "," + SubmitSurveys + "," + ReviewSurveys;

    /// <summary>
    /// Create endpoint: FieldTeam holds <see cref="CreateSurveys"/>; Dispatcher keeps working with
    /// <see cref="ManageAssignments"/> alone so existing grants are not broken.
    /// </summary>
    public const string CreateOrManageAssignments =
        PermissionPolicies.AnyPrefix + CreateSurveys + "," + ManageAssignments;

    public static readonly IReadOnlyList<string> All =
    [
        ManageTemplates,
        ViewTemplates,
        ViewSurveys,
        ManageAssignments,
        CreateSurveys,
        SubmitSurveys,
        ReviewSurveys,
        ManageTeams,
        ManageLookups,
        ManageUsers,
        ViewDashboard,
        ViewReports,
        ImportData
    ];
}
