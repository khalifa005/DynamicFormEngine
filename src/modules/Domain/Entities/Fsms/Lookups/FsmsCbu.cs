namespace KH.Domain.Entities.Fsms.Lookups;

/// <summary>
/// A customer business unit (RCBU, JCBU, …) — the second level of the geography, sitting under a
/// <see cref="FsmsCluster"/> and owning the task branches beneath it. Mirrored from the WFM city/CBU
/// reference feed. Table: <c>LKP_CBU</c>.
/// </summary>
public sealed class FsmsCbu : BaseEntity<int>
{
    public string Code { get; set; } = default!;

    /// <summary>Parent <see cref="FsmsCluster.Code"/>.</summary>
    public string ClusterCode { get; set; } = default!;

    public string NameEn { get; set; } = default!;
    public string NameAr { get; set; } = default!;

    /// <summary>WFM <c>ORGID</c> — carried so inbound payloads can be matched back to the CBU.</summary>
    public long? OrgId { get; set; }

    /// <summary>WFM <c>ORGCODE</c>.</summary>
    public string? OrgCode { get; set; }

    /// <summary>WFM <c>DEFAULTWFMZONE</c> — the task branch work lands in when none is supplied.</summary>
    public string? DefaultTaskZone { get; set; }

    public bool IsActive { get; set; } = true;
}
