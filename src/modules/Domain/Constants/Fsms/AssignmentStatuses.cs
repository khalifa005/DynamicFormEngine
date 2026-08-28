namespace KH.Domain.Constants.Fsms;

/// <summary>
/// Lifecycle states of a field-team assignment. Owned by later phases (task-04+); declared here
/// so downstream slices share the same constants.
/// </summary>
public abstract class AssignmentStatuses
{
    public const string Pending = "PENDING";
    public const string InProgress = "IN_PROGRESS";
    public const string Submitted = "SUBMITTED";

    /// <summary>
    /// Legacy. Set by the retired receive step, which marked the crew's allocation reviewed as the
    /// survey moved to <c>UNDER_REVIEW</c>. Kept so allocations written before the change stay
    /// recognized; nothing sets it now.
    /// </summary>
    public const string Reviewed = "REVIEWED";

    public const string Approved = "APPROVED";
    public const string Returned = "RETURNED";
    public const string Expired = "EXPIRED";

    /// <summary>
    /// The survey was allocated to a different team while this allocation was still live. Terminal
    /// — the work moved on, so the row is kept for the trail rather than deleted.
    /// </summary>
    public const string Reassigned = "REASSIGNED";

    public static readonly IReadOnlyList<string> All =
    [
        Pending,
        InProgress,
        Submitted,
        Reviewed,
        Approved,
        Returned,
        Expired,
        Reassigned
    ];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
