namespace KH.Domain.Entities.Fsms.Lookups;

/// <summary>
/// Customer class / type reference (WFM <c>customerClass</c>). Table: <c>LKP_CUSTOMER_TYPE</c>.
/// </summary>
public sealed class FsmsCustomerType : BaseEntity<long>
{
    public string Code { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}
