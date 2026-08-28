namespace KH.Domain.Entities.Fsms.Lookups;

/// <summary>
/// Department reference. Mocked from WFM until task-09. Table: <c>LKP_DEPARTMENT</c>.
/// </summary>
public sealed class FsmsDepartment : BaseEntity<int>
{
    public string NameEn { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}
