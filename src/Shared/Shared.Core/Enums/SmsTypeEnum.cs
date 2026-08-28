namespace Shared.Core.Enums;

public sealed class SmsTypeEnum(int id, string nameAr, string nameEn)
    : DynamicEnumItem<int>(id, nameAr, nameEn)
{
    public static readonly SmsTypeEnum SMS = new(1, "رسالة نصية", "SMS");
    public static readonly SmsTypeEnum Email = new(2, "بريد إلكتروني", "Email");
    public static readonly SmsTypeEnum Both = new(3, "بريد إلكتروني و رسالة نصية", "Both");

    public static SmsTypeEnum Default => SMS;

    private static readonly List<SmsTypeEnum> _allRoles = [SMS, Email, Both];

    public static IReadOnlyList<SmsTypeEnum> GetAll() => _allRoles.AsReadOnly();

    public static SmsTypeEnum? GetById(int id) =>
        _allRoles.Find(r => r.Id.CompareTo(id) == 0);
}
