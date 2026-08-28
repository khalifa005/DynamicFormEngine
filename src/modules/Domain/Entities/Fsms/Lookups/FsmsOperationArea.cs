namespace KH.Domain.Entities.Fsms.Lookups;

/// <summary>
/// One of the two ways FSMS scopes work at the narrowest level — a sub-maintenance area hanging
/// directly off a <see cref="FsmsCbu"/>, alongside (not beneath) <see cref="FsmsBranch"/>. The two
/// are siblings: an operation area is not part of any one branch, so a branch scope does not reach
/// it and only the CBU above covers both.
///
/// Upstream this sits below a maintenance area; that intermediate level is not modelled on its own
/// because nothing scopes to it, so its code is kept here as <see cref="MainAreaCode"/> rather than
/// being lost. Table: <c>LKP_OPERATION_AREA</c>.
/// </summary>
public sealed class FsmsOperationArea : BaseEntity<long>
{
    public string Code { get; set; } = default!;

    /// <summary>Parent <see cref="FsmsCbu.Code"/>.</summary>
    public string CbuCode { get; set; } = default!;

    /// <summary>WFM <c>MAINAREA_CODE</c> — the maintenance area this operation area belongs to.</summary>
    public string? MainAreaCode { get; set; }

    public string NameEn { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}
