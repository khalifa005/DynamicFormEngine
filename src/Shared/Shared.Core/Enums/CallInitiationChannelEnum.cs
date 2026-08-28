namespace Shared.Core.Enums;

public sealed class CallInitiationChannelEnum(int id, string nameAr, string nameEn, string code)
    : DynamicEnumItem<int>(id, nameAr, nameEn, code)
{
    public static readonly CallInitiationChannelEnum FieldTeam = new(1, "فرق ميدانية", "Field Team", "FieldTeam");
    public static readonly CallInitiationChannelEnum DriverMobile = new(2, "تطبيق السائق", "Driver Mobile", "DriverMobile");
    public static readonly CallInitiationChannelEnum BackOfficeMobile = new(3, "مكتب خلفي - جوال", "Back Office Mobile", "BackOfficeMobile");
    public static CallInitiationChannelEnum Default => FieldTeam;

    private static readonly List<CallInitiationChannelEnum> _all =
        [FieldTeam, DriverMobile, BackOfficeMobile];

    public static IReadOnlyList<CallInitiationChannelEnum> GetAll() => _all.AsReadOnly();

    public static CallInitiationChannelEnum? GetById(int id) =>
        _all.Find(x => x.Id.CompareTo(id) == 0);

    public static CallInitiationChannelEnum? GetByCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var trimmed = code.Trim();
        return _all.Find(x =>
            string.Equals(x.Code, trimmed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.NameEn, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
