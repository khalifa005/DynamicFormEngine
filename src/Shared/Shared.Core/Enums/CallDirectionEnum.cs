namespace Shared.Core.Enums;

public sealed class CallDirectionEnum(int id, string nameAr, string nameEn, string code)
    : DynamicEnumItem<int>(id, nameAr, nameEn, code)
{
    public static readonly CallDirectionEnum Inbound = new(1, "وارد", "Inbound", "Inbound");
    public static readonly CallDirectionEnum Outbound = new(2, "صادر", "Outbound", "Outbound");
    public static CallDirectionEnum Default => Outbound;

    private static readonly List<CallDirectionEnum> _all = [Inbound, Outbound];

    public static IReadOnlyList<CallDirectionEnum> GetAll() => _all.AsReadOnly();

    public static CallDirectionEnum? GetById(int id) =>
        _all.Find(x => x.Id.CompareTo(id) == 0);

    public static CallDirectionEnum? GetByCode(string? code)
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
