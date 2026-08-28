namespace KH.Domain.Constants.Fsms;

/// <summary>
/// Lifecycle states of a survey template master record.
/// </summary>
public abstract class TemplateStatuses
{
    public const string Draft = "DRAFT";
    public const string Published = "PUBLISHED";
    public const string Deprecated = "DEPRECATED";
    public const string Archived = "ARCHIVED";

    public static readonly IReadOnlyList<string> All =
    [
        Draft,
        Published,
        Deprecated,
        Archived
    ];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
