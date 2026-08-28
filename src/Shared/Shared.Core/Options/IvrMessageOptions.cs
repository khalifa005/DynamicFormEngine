namespace Shared.Core.Options;

public sealed class IvrMessageOptions
{
    public string En { get; set; } = string.Empty;

    public string Ar { get; set; } = string.Empty;
}

public static class IvrMessageDefaults
{
    public const string SessionSelectionEn = "Please select your session.";
    public const string SessionSelectionAr = "يرجى اختيار الجلسة.";
    public const string NoActiveSessionEn = "No active session found.";
    public const string NoActiveSessionAr = "لا توجد جلسة نشطة.";
    public const string SessionExpiredEn = "This session has expired.";
    public const string SessionExpiredAr = "انتهت صلاحية هذه الجلسة.";
    public const string SessionConsumedEn = "This session has already been used.";
    public const string SessionConsumedAr = "تم استخدام هذه الجلسة مسبقاً.";
    public const string MaxSessionsReachedEn = "Maximum active sessions reached.";
    public const string MaxSessionsReachedAr = "تم الوصول إلى الحد الأقصى للجلسات النشطة.";
    public const string ActiveSessionExistsEn = "An active session already exists.";
    public const string ActiveSessionExistsAr = "توجد جلسة نشطة بالفعل.";
    public const string TwoWayNotAllowedEn = "Two-way session is not allowed.";
    public const string TwoWayNotAllowedAr = "الاتصال ثنائي الاتجاه غير مسموح.";
    public const string InvalidReferenceTypeEn = "Invalid reference type.";
    public const string InvalidReferenceTypeAr = "نوع المرجع غير صالح.";
    public const string RecordingMessageEn = "This call may be recorded for quality purposes.";
    public const string RecordingMessageAr = "قد يتم تسجيل هذه المكالمة لأغراض الجودة.";
}
