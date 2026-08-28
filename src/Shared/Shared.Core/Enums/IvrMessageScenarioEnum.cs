namespace Shared.Core.Enums;

public sealed class IvrMessageScenarioEnum(int id, string nameAr, string nameEn, string code)
    : DynamicEnumItem<int>(id, nameAr, nameEn, code)
{
    public static readonly IvrMessageScenarioEnum SessionSelection = new(1, "اختيار الجلسة", "Session selection", "SessionSelection");
    public static readonly IvrMessageScenarioEnum NoActiveSession = new(2, "لا توجد جلسة نشطة", "No active session", "NoActiveSession");
    public static readonly IvrMessageScenarioEnum SessionExpired = new(3, "انتهت الجلسة", "Session expired", "SessionExpired");
    public static readonly IvrMessageScenarioEnum SessionConsumed = new(4, "استُهلكت الجلسة", "Session consumed", "SessionConsumed");
    public static readonly IvrMessageScenarioEnum MaxSessionsReached = new(5, "الحد الأقصى للجلسات", "Max sessions reached", "MaxSessionsReached");
    public static readonly IvrMessageScenarioEnum ActiveSessionExists = new(6, "جلسة نشطة موجودة", "Active session exists", "ActiveSessionExists");
    public static readonly IvrMessageScenarioEnum TwoWayNotAllowed = new(7, "الاتصال ثنائي الاتجاه غير مسموح", "Two-way not allowed", "TwoWayNotAllowed");
    public static readonly IvrMessageScenarioEnum InvalidReferenceType = new(8, "نوع مرجع غير صالح", "Invalid reference type", "InvalidReferenceType");
    public static readonly IvrMessageScenarioEnum RecordingMessage = new(9, "رسالة التسجيل", "Recording message", "RecordingMessage");
    public static IvrMessageScenarioEnum Default => SessionSelection;

    private static readonly List<IvrMessageScenarioEnum> _all =
    [
        SessionSelection,
        NoActiveSession,
        SessionExpired,
        SessionConsumed,
        MaxSessionsReached,
        ActiveSessionExists,
        TwoWayNotAllowed,
        InvalidReferenceType,
        RecordingMessage
    ];

    public static IReadOnlyList<IvrMessageScenarioEnum> GetAll() => _all.AsReadOnly();

    public static IvrMessageScenarioEnum? GetByCode(string? code)
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
