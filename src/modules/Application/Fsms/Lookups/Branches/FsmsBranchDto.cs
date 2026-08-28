namespace KH.Application.Fsms.Lookups.Branches;

public sealed class FsmsBranchDto
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? TaskZone { get; init; }
    public string? BranchCode { get; init; }

    /// <summary>Parent <see cref="KH.Domain.Entities.Fsms.Lookups.FsmsCbu.Code"/>.</summary>
    public string? CbuCode { get; init; }

    public bool IsActive { get; init; }
}
