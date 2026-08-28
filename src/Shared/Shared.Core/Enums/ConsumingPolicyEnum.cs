namespace Shared.Core.Enums;

public sealed class ConsumingPolicyEnum(int id, string nameAr, string nameEn, string code)
    : DynamicEnumItem<int>(id, nameAr, nameEn, code)
{
    public static readonly ConsumingPolicyEnum OnSessionUsed = new(1, "عند استخدام الجلسة", "On session used", "OnSessionUsed");
    public static readonly ConsumingPolicyEnum OnAnswered = new(2, "عند الرد", "On answered", "OnAnswered");
    public static ConsumingPolicyEnum Default => OnSessionUsed;

    private static readonly List<ConsumingPolicyEnum> _all =
    [
        OnSessionUsed, OnAnswered
    ];

    public static IReadOnlyList<ConsumingPolicyEnum> GetAll() => _all.AsReadOnly();

    public static ConsumingPolicyEnum? GetById(int id) =>
        _all.Find(x => x.Id.CompareTo(id) == 0);

    public static ConsumingPolicyEnum? GetByCode(string? code)
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
