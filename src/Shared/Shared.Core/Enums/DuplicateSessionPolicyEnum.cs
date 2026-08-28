namespace Shared.Core.Enums;

public sealed class DuplicateSessionPolicyEnum(int id, string nameAr, string nameEn, string code)
    : DynamicEnumItem<int>(id, nameAr, nameEn, code)
{
    public static readonly DuplicateSessionPolicyEnum CloseAndReplace = new(1, "إغلاق واستبدال", "Close and replace", "CloseAndReplace");
    public static readonly DuplicateSessionPolicyEnum Reuse = new(2, "إعادة استخدام", "Reuse", "Reuse");
    public static readonly DuplicateSessionPolicyEnum Reject = new(3, "رفض", "Reject", "Reject");
    public static DuplicateSessionPolicyEnum Default => CloseAndReplace;

    private static readonly List<DuplicateSessionPolicyEnum> _all =
    [
        CloseAndReplace, Reuse, Reject
    ];

    public static IReadOnlyList<DuplicateSessionPolicyEnum> GetAll() => _all.AsReadOnly();

    public static DuplicateSessionPolicyEnum? GetById(int id) =>
        _all.Find(x => x.Id.CompareTo(id) == 0);

    public static DuplicateSessionPolicyEnum? GetByCode(string? code)
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
