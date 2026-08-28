namespace KH.Application.Fsms.Lookups.CustomerTypes;

public sealed class FsmsCustomerTypeDto
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
