namespace KH.Domain.Entities.Fsms.Lookups;

/// <summary>
/// FA type / task type reference. Mocked from WFM until task-09. Table: <c>LKP_FA_TYPE</c>.
/// </summary>
public sealed class FsmsFaType : BaseEntity<long>
{
    public string FaTypeCode { get; set; } = default!;
    public long TaskTypeId { get; set; }
    public string NameEn { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}
