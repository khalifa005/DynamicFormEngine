namespace KH.Application.Fsms.Lookups.FaTypes;

public sealed class FsmsFaTypeDto
{
    public long Id { get; init; }
    public string FaTypeCode { get; init; } = string.Empty;
    public long TaskTypeId { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
