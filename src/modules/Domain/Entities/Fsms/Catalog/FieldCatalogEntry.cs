namespace KH.Domain.Entities.Fsms.Catalog;

/// <summary>
/// A globally shared, canonical field definition. Lets the form builder reuse consistent
/// <c>data_name</c>s across templates instead of creating near-duplicate columns. One canonical
/// <see cref="FieldType"/> per <see cref="DataName"/>. Table: <c>FIELD_CATALOG</c>.
/// </summary>
public sealed class FieldCatalogEntry : BaseAuditableEntity<long>
{
    private FieldCatalogEntry()
    {
    }

    private FieldCatalogEntry(string dataName, string fieldType, string? labelEn, string? labelAr, string? description)
    {
        DataName = dataName;
        FieldType = fieldType;
        LabelEn = labelEn;
        LabelAr = labelAr;
        Description = description;
        IsActive = true;
    }

    /// <summary>Canonical, unique key (matches the builder field's <c>data_name</c>).</summary>
    public string DataName { get; private set; } = default!;

    /// <summary>Canonical builder element type (e.g. <c>text</c>, <c>numeric</c>, <c>single_choice</c>).</summary>
    public string FieldType { get; private set; } = default!;

    public string? LabelEn { get; private set; }
    public string? LabelAr { get; private set; }
    public string? Description { get; private set; }

    public static FieldCatalogEntry Create(string dataName, string fieldType, string? labelEn = null, string? labelAr = null, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(dataName))
        {
            throw new DomainException("A catalog entry must have a data name.");
        }

        if (string.IsNullOrWhiteSpace(fieldType))
        {
            throw new DomainException("A catalog entry must have a field type.");
        }

        return new FieldCatalogEntry(dataName.Trim(), fieldType.Trim(), labelEn?.Trim(), labelAr?.Trim(), description?.Trim());
    }

    public void UpdateLabels(string? labelEn, string? labelAr, string? description)
    {
        LabelEn = labelEn?.Trim();
        LabelAr = labelAr?.Trim();
        Description = description?.Trim();
    }

    /// <summary>
    /// Changes the canonical builder type. Used when a seed or publish discovers the name was
    /// registered under the wrong type (e.g. <c>address</c> as text vs geolocation).
    /// </summary>
    public void ChangeType(string fieldType)
    {
        if (string.IsNullOrWhiteSpace(fieldType))
        {
            throw new DomainException("A catalog entry must have a field type.");
        }

        FieldType = fieldType.Trim();
    }
}
