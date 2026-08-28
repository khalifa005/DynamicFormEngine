namespace KH.Domain.Constants.Fsms;

/// <summary>
/// Lifecycle states of a survey instance. Owned by later phases (task-03+); declared here so
/// downstream slices share the same constants.
/// </summary>
public abstract class SurveyStatuses
{
    public const string Created = "CREATED";
    public const string Assigned = "ASSIGNED";
    public const string InProgress = "IN_PROGRESS";
    public const string Submitted = "SUBMITTED";

    /// <summary>
    /// Legacy. A filled survey used to be "received" into this state before the back office could
    /// complete or return it; that step was removed and <see cref="Submitted"/> is now the
    /// awaiting-review state. Kept because status history and surveys written before the change
    /// still carry it, so it must stay recognized by filters and by <see cref="IsDefined"/>.
    /// </summary>
    public const string UnderReview = "UNDER_REVIEW";

    public const string Approved = "APPROVED";
    public const string Returned = "RETURNED";
    public const string Expired = "EXPIRED";

    public static readonly IReadOnlyList<string> All =
    [
        Created,
        Assigned,
        InProgress,
        Submitted,
        UnderReview,
        Approved,
        Returned,
        Expired
    ];

    /// <summary>
    /// The states in which a survey still holds no answers, and so the only ones an automatic
    /// republish may re-pin to a newer template version. A survey that has already been filled keeps
    /// its pinned version: its answers were given against that form, and swapping the form under a
    /// reviewer would misdescribe them. Declared as a list so EF Core translates
    /// <c>Contains</c> into a SQL <c>IN</c> clause.
    /// </summary>
    public static readonly IReadOnlyList<string> AutoMigratable =
    [
        Created,
        Assigned,
        InProgress
    ];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
