namespace KH.Domain.Constants.Fsms;

/// <summary>
/// Client shapes a published template version can be assembled for. One version snapshot row is
/// produced per target client on publish.
/// </summary>
public abstract class TargetClients
{
    public const string Formly = "Formly";
    public const string Mobile = "Mobile";

    public static readonly IReadOnlyList<string> All =
    [
        Formly,
        Mobile
    ];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
