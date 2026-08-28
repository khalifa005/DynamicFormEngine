namespace KH.Application.Common.Options;

/// <summary>
/// Binds the <c>DataMigration</c> configuration section.
///
/// There is no per-source media configuration and no copying. A historical archive is placed under
/// the file-storage root once, in bulk, by whoever moves it across; the importer then references
/// each file where it lies. Because the archive sits inside the same root as everything else, a
/// migrated file is read by the ordinary download, thumbnail and PDF paths with no special case.
/// </summary>
public sealed class DataMigrationOptions
{
    public const string SectionName = "DataMigration";

    /// <summary>Largest accepted workbook, in megabytes.</summary>
    public int MaxUploadMb { get; init; } = 50;

    /// <summary>
    /// Folder under the file-storage root holding the migration archive — one flat folder, named by
    /// the ids the source data carries. Flat on purpose: the operator has nothing to type and ops
    /// has no subfolders to create, and the ids are GUIDs, so a batch dropped in later cannot
    /// collide with one already there.
    /// </summary>
    public string ArchiveFolder { get; init; } = "archive";

    /// <summary>
    /// Extensions to try against a bare media key, dot-prefixed and lower-case. An export names its
    /// files by id and records the type nowhere in its data, so the extension has to be discovered.
    /// </summary>
    public IReadOnlyList<string> AllowedExtensions { get; init; } = [".jpg", ".jpeg", ".png", ".mp4"];
}
