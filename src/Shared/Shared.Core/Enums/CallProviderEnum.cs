namespace Shared.Core.Enums;

public sealed class CallProviderEnum(int id, string nameAr, string nameEn, string code)
    : DynamicEnumItem<int>(id, nameAr, nameEn, code)
{
    public static readonly CallProviderEnum Zoom = new(1, "زوم", "Zoom", "Zoom");
    public static readonly CallProviderEnum Ivr = new(2, "الرد الآلي", "Ivr", "Ivr");
    public static CallProviderEnum Default => Ivr;

    private static readonly List<CallProviderEnum> _all = [Zoom, Ivr];

    public static IReadOnlyList<CallProviderEnum> GetAll() => _all.AsReadOnly();

    public static CallProviderEnum? GetById(int id) =>
        _all.Find(x => x.Id.CompareTo(id) == 0);

    public static CallProviderEnum? GetByCode(string? code)
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
