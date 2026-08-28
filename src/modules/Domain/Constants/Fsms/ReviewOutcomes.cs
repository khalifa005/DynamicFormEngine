namespace KH.Domain.Constants.Fsms;

/// <summary>
/// Possible outcomes a reviewer can record on a submitted survey.
/// </summary>
public abstract class ReviewOutcomes
{
    public const string Reviewed = "REVIEWED";
    public const string Approved = "APPROVED";
    public const string Returned = "RETURNED";

    public static readonly IReadOnlyList<string> All =
    [
        Reviewed,
        Approved,
        Returned
    ];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
