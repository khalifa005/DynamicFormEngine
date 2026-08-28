namespace KH.Domain.Entities.Fsms.Lookups;

/// <summary>
/// Top of the geography: a cluster groups the CBUs of one region (Central, Western, …).
/// Mirrored from the WFM <c>CLUSTERCODE</c> / <c>CLUSTERNAMEEN</c> columns. Table: <c>LKP_CLUSTER</c>.
/// </summary>
public sealed class FsmsCluster : BaseEntity<int>
{
    public string Code { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}
