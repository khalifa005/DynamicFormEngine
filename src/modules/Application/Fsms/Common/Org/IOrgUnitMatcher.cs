namespace KH.Application.Fsms.Common.Org;

/// <summary>
/// Where an inbound field activity landed, once its codes have been checked against our own
/// geography. Any part may be null — an upstream system is not obliged to name a place we know.
/// </summary>
/// <param name="Unmatched">
/// The codes that were sent but matched nothing, in the order CBU, branch, operation area. Present
/// so the caller can log what was dropped instead of the miss being invisible.
/// </param>
public sealed record OrgUnitMatch(
    string? CbuCode,
    string? BranchCode,
    string? OperationAreaCode,
    IReadOnlyList<string> Unmatched)
{
    public static readonly OrgUnitMatch None = new(null, null, null, []);

    /// <summary>True when at least one level resolved, i.e. the survey can be scoped at all.</summary>
    public bool HasAny => CbuCode is not null || BranchCode is not null || OperationAreaCode is not null;
}

/// <summary>
/// Turns the org codes on an inbound WFM or C2M field activity into the codes FSMS stamps a survey
/// with, so both intake paths place work the same way.
/// </summary>
/// <remarks>
/// Deliberately forgiving. Upstream reference data drifts from ours, and refusing a field activity
/// because a branch code is unknown would leave real work unraised in both systems. An unmatched
/// code is dropped and reported through <see cref="OrgUnitMatch.Unmatched"/>; the survey is still
/// created, just unscoped at that level, and shows up in the back office as work to place by hand.
/// </remarks>
public interface IOrgUnitMatcher
{
    Task<OrgUnitMatch> MatchAsync(
        string? cbuCode,
        string? branchCode,
        string? operationAreaCode,
        CancellationToken cancellationToken);
}
