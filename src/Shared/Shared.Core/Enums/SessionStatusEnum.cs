namespace Shared.Core.Enums;

public sealed class SessionStatusEnum(int id, string nameAr, string nameEn, string code)
    : DynamicEnumItem<int>(id, nameAr, nameEn, code)
{
    public static readonly SessionStatusEnum Active = new(1, "نشط", "Active", "Active");
    public static readonly SessionStatusEnum Consumed = new(2, "مستهلك", "Consumed", "Consumed");
    public static readonly SessionStatusEnum Completed = new(3, "مكتمل", "Completed", "Completed");
    public static readonly SessionStatusEnum Failed = new(4, "فشل", "Failed", "Failed");
    public static readonly SessionStatusEnum Expired = new(5, "منتهي", "Expired", "Expired");
    public static readonly SessionStatusEnum Closed = new(6, "مغلق", "Closed", "Closed");
    public static SessionStatusEnum Default => Active;

    private static readonly List<SessionStatusEnum> _all =
    [
        Active, Consumed, Completed, Failed, Expired, Closed
    ];

    public static IReadOnlyList<SessionStatusEnum> GetAll() => _all.AsReadOnly();

    public static SessionStatusEnum? GetById(int id) =>
        _all.Find(x => x.Id.CompareTo(id) == 0);

    public static SessionStatusEnum? GetByCode(string? code)
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
