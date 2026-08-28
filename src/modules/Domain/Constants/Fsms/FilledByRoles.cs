namespace KH.Domain.Constants.Fsms;

/// <summary>
/// Who filled a survey submission. A single survey accepts more than one fill — the field team
/// answers its part on site and the back office/reviewer answers its own part — so every
/// submission row records the role behind it and the survey's result summary is keyed by it.
/// </summary>
public abstract class FilledByRoles
{
    public const string FieldTeam = "FIELD_TEAM";
    public const string BackOffice = "BACK_OFFICE";

    public static readonly IReadOnlyList<string> All =
    [
        FieldTeam,
        BackOffice
    ];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
