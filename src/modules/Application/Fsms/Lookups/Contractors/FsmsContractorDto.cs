namespace KH.Application.Fsms.Lookups.Contractors;

public sealed class FsmsContractorDto
{
    public int Id { get; init; }
    public string PoNumber { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? CommercialRegistration { get; init; }
    public bool IsActive { get; init; }
}
