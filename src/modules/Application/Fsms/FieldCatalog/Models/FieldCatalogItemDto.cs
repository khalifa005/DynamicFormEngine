namespace KH.Application.Fsms.FieldCatalog.Models;

public sealed class FieldCatalogItemDto
{
    public long CatalogId { get; init; }
    public string DataName { get; init; } = default!;
    public string FieldType { get; init; } = default!;
    public string? LabelEn { get; init; }
    public string? LabelAr { get; init; }
    public string? Description { get; init; }
}
