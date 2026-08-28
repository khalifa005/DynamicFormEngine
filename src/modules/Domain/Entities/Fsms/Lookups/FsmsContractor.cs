namespace KH.Domain.Entities.Fsms.Lookups;

/// <summary>
/// Contractor / blanket PO from WFM <c>EAMNWC_WFM_CONTRACTOR_NCONN_V</c>.
/// Natural key is the PO number, not a vendor company id. Table: <c>LKP_CONTRACTOR</c>.
/// </summary>
public sealed class FsmsContractor : BaseEntity<int>
{
    public const int PoNumberMaxLength = 50;
    public const int NameMaxLength = 250;
    public const int CommercialRegistrationMaxLength = 50;

    public string PoNumber { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string? CommercialRegistration { get; set; }
    public bool IsActive { get; set; } = true;
}
