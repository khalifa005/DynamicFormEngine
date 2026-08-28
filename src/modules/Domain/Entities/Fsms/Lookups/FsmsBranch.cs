namespace KH.Domain.Entities.Fsms.Lookups;

/// <summary>
/// A branch (WFM <c>TASKZONE</c> / <c>WORKLOC</c>) — one of the two levels beneath a
/// <see cref="FsmsCbu"/>, alongside <see cref="FsmsOperationArea"/> rather than above it.
/// Table: <c>LKP_BRANCH</c>.
///
/// The cluster is deliberately not stored here: it is reachable through
/// <see cref="CbuCode"/> → <see cref="FsmsCbu.ClusterCode"/>, and a second copy of it on this row
/// would drift the first time a CBU moved between clusters.
/// </summary>
public sealed class FsmsBranch : BaseEntity<long>
{
    /// <summary>The canonical identifier FSMS scopes and stamps work with (WFM's task-zone number).</summary>
    public string Code { get; set; } = default!;

    /// <summary>Parent <see cref="FsmsCbu.Code"/>.</summary>
    public string? CbuCode { get; set; }

    public string? TaskZone { get; set; }

    /// <summary>
    /// The branch's own upstream label (<c>R-16</c>, <c>TC-01</c>) — what WFM and C2M put in the
    /// <c>branchCode</c> field of an inbound field activity. Held as an alias of <see cref="Code"/>
    /// rather than replacing it, because existing surveys and scopes are stamped with the code.
    /// </summary>
    public string? BranchCode { get; set; }
    public string NameEn { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}
