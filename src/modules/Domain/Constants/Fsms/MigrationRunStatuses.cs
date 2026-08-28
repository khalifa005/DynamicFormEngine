namespace KH.Domain.Constants.Fsms;

/// <summary>
/// Lifecycle of one import run. A run is queued by the request that uploaded the file and worked
/// through by a background job, so the page that started it only ever learns how far it got by
/// reading this back.
/// </summary>
public abstract class MigrationRunStatuses
{
    /// <summary>Queued; the job has not picked it up yet.</summary>
    public const string Pending = "PENDING";

    public const string Running = "RUNNING";

    /// <summary>Finished. Individual records may still have failed — see the per-record rows.</summary>
    public const string Completed = "COMPLETED";

    /// <summary>The run itself could not proceed (unreadable file, missing media root, …).</summary>
    public const string Failed = "FAILED";

    public static readonly IReadOnlyList<string> All =
    [
        Pending,
        Running,
        Completed,
        Failed
    ];

    /// <summary>Statuses a run no longer moves out of; the client stops polling on these.</summary>
    public static readonly IReadOnlyList<string> Terminal =
    [
        Completed,
        Failed
    ];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);

    public static bool IsTerminal(string? value) =>
        value is not null && Terminal.Contains(value, StringComparer.Ordinal);
}
