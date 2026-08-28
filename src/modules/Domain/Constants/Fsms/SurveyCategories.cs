namespace KH.Domain.Constants.Fsms;

/// <summary>
/// Business categories a survey template can belong to.
/// </summary>
public abstract class SurveyCategories
{
    public const string AssetInspection = "ASSET_INSPECTION";
    public const string Quality = "QUALITY";
    public const string Customer = "CUSTOMER";
    public const string Environmental = "ENVIRONMENTAL";
    public const string Safety = "SAFETY";
    public const string Custom = "CUSTOM";

    public static readonly IReadOnlyList<string> All =
    [
        AssetInspection,
        Quality,
        Customer,
        Environmental,
        Safety,
        Custom
    ];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
