namespace Shared.Core.Enums;

public sealed class SessionOperationEnum(int id, string nameAr, string nameEn, string code)
    : DynamicEnumItem<int>(id, nameAr, nameEn, code)
{
    public static readonly SessionOperationEnum Create = new(1, "إنشاء", "Create", "Create");
    public static readonly SessionOperationEnum Update = new(2, "تحديث", "Update", "Update");
    public static readonly SessionOperationEnum Close = new(3, "إغلاق", "Close", "Close");
    public static readonly SessionOperationEnum Inquire = new(4, "استعلام", "Inquire", "Inquire");
    public static readonly SessionOperationEnum Webhook = new(5, "ويب هوك", "Webhook", "Webhook");
    public static SessionOperationEnum Default => Create;

    private static readonly List<SessionOperationEnum> _all =
    [
        Create, Update, Close, Inquire, Webhook
    ];

    public static IReadOnlyList<SessionOperationEnum> GetAll() => _all.AsReadOnly();

    public static SessionOperationEnum? GetById(int id) =>
        _all.Find(x => x.Id.CompareTo(id) == 0);

    public static SessionOperationEnum? GetByCode(string? code)
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
