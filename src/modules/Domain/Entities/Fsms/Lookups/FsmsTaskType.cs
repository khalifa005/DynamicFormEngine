namespace KH.Domain.Entities.Fsms.Lookups;

/// <summary>
/// Task type reference. Ids are WFM-mirrored until task-09. Table: <c>LKP_TASK_TYPE</c>.
/// </summary>
public sealed class FsmsTaskType : BaseEntity<long>
{
    public string Code { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}
