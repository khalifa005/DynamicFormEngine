namespace KH.Domain.Constants.Fsms;

/// <summary>What became of one record in an import run.</summary>
public abstract class MigrationRecordStatuses
{
    /// <summary>A survey, its submission and its media were written.</summary>
    public const string Imported = "IMPORTED";

    /// <summary>
    /// Already present from an earlier run. Re-running the same file is the normal way to finish an
    /// import that was interrupted, so this is an expected outcome rather than a problem.
    /// </summary>
    public const string Skipped = "SKIPPED";

    public const string Failed = "FAILED";

    /// <summary>Read and checked but deliberately not written — a validate-only run.</summary>
    public const string Validated = "VALIDATED";

    public static readonly IReadOnlyList<string> All =
    [
        Imported,
        Skipped,
        Failed,
        Validated
    ];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
