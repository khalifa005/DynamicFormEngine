namespace KH.Domain.Constants.Fsms;

/// <summary>
/// Lifecycle of an uploaded survey media file. A file is uploaded the moment the user picks it
/// (<see cref="Pending"/>) and is only tied to a submission when the form is submitted
/// (<see cref="Linked"/>). Files left <see cref="Pending"/> past a cutoff are orphans and can be
/// swept without touching submitted data.
/// </summary>
public abstract class SubmissionFileStatuses
{
    public const string Pending = "PENDING";
    public const string Linked = "LINKED";

    public static readonly IReadOnlyList<string> All =
    [
        Pending,
        Linked
    ];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
