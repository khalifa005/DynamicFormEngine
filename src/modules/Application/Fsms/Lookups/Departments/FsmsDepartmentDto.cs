namespace KH.Application.Fsms.Lookups.Departments;

public sealed class FsmsDepartmentDto
{
    public int Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
