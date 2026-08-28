namespace Shared.Core.Enums;

public sealed class CallDispositionEnum(int id, string nameAr, string nameEn, string code)
    : DynamicEnumItem<int>(id, nameAr, nameEn, code)
{
    public static readonly CallDispositionEnum Answered = new(1, "تم الرد", "Answered", "Answered");
    public static readonly CallDispositionEnum NoAnswer = new(2, "لا رد", "No Answer", "NoAnswer");
    public static readonly CallDispositionEnum Busy = new(3, "مشغول", "Busy", "Busy");
    public static readonly CallDispositionEnum Failed = new(4, "فشل", "Failed", "Failed");
    public static readonly CallDispositionEnum Cancelled = new(5, "ملغى", "Cancelled", "Cancelled");
    public static readonly CallDispositionEnum Unknown = new(6, "غير معروف", "Unknown", "Unknown");
    public static CallDispositionEnum Default => Unknown;

    private static readonly List<CallDispositionEnum> _all =
        [Answered, NoAnswer, Busy, Failed, Cancelled, Unknown];

    public static IReadOnlyList<CallDispositionEnum> GetAll() => _all.AsReadOnly();

    public static CallDispositionEnum? GetById(int id) =>
        _all.Find(x => x.Id.CompareTo(id) == 0);

    public static CallDispositionEnum? GetByCode(string? code)
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
