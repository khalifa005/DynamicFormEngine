namespace KH.Application.Fsms.Lookups.OperationAreas;

public sealed class FsmsOperationAreaDto
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;

    /// <summary>Parent <see cref="KH.Domain.Entities.Fsms.Lookups.FsmsCbu.Code"/>.</summary>
    public string CbuCode { get; init; } = string.Empty;

    public string? MainAreaCode { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
