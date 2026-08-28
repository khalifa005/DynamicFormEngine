namespace KH.Application.Fsms.Lookups.Clusters;

public sealed class FsmsClusterDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
