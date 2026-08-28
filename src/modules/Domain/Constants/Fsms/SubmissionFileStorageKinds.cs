namespace KH.Domain.Constants.Fsms;

/// <summary>
/// Who owns the bytes behind a <c>SubmissionFile</c> row.
///
/// <see cref="Managed"/> files were written by this application — an upload or a signature — so it is
/// free to move them when a submission claims them and to delete them when the row goes.
/// <see cref="Migrated"/> files came from a historical archive that was placed on the server in bulk
/// and is only referenced: an archive can run to a terabyte, and copying it through the app would
/// hold two of it while a single delete could destroy the master copy.
///
/// The distinction exists precisely because those two verbs — move and delete — are wrong for a
/// migrated file and right for a managed one. Reading is identical for both, which is why every
/// download, thumbnail and PDF path needs no knowledge of this at all.
/// </summary>
public abstract class SubmissionFileStorageKinds
{
    /// <summary>Written by this application; movable and deletable.</summary>
    public const string Managed = "MANAGED";

    /// <summary>From the migration archive, referenced in place; never moved, never deleted.</summary>
    public const string Migrated = "MIGRATED";

    public static readonly IReadOnlyList<string> All =
    [
        Managed,
        Migrated
    ];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
