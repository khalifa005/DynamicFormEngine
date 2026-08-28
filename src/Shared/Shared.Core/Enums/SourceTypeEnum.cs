namespace Shared.Core.Enums;

public sealed class SourceTypeEnum(int id, string nameAr, string nameEn)
    : DynamicEnumItem<int>(id, nameAr, nameEn)
{
    public static readonly SourceTypeEnum WFM = new(1, "WFM", "WFM");
    public static readonly SourceTypeEnum TMS = new(2, "TMS", "TMS");
    public static SourceTypeEnum Default => TMS;

    private static readonly List<SourceTypeEnum> _allSources = [WFM, TMS];

    public static IReadOnlyList<SourceTypeEnum> GetAll() => _allSources.AsReadOnly();

    public static SourceTypeEnum? GetById(int id) =>
        _allSources.Find(r => r.Id.CompareTo(id) == 0);
}
