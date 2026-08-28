namespace KH.Application.Fsms.Lookups.Cbus;

public sealed class FsmsCbuDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;

    /// <summary>Parent <see cref="KH.Domain.Entities.Fsms.Lookups.FsmsCluster.Code"/>.</summary>
    public string ClusterCode { get; init; } = string.Empty;

    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public long? OrgId { get; init; }
    public string? OrgCode { get; init; }
    public string? DefaultTaskZone { get; init; }
    public bool IsActive { get; init; }
}
