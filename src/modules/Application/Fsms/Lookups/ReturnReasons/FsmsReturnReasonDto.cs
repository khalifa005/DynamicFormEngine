namespace KH.Application.Fsms.Lookups.ReturnReasons;

public sealed class FsmsReturnReasonDto
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}
