namespace KH.Domain.Constants.Fsms;

/// <summary>
/// How far an import run goes. <see cref="Validate"/> walks the whole file and reports exactly what
/// an import would do — record count, unreadable rows, media found and missing — without writing a
/// thing, so an operator can find a bad export before it becomes several hundred surveys to undo.
/// </summary>
public abstract class MigrationModes
{
    public const string Validate = "VALIDATE";
    public const string Import = "IMPORT";

    /// <summary>
    /// Attaches media to surveys that were imported before their files reached the archive.
    ///
    /// A separate pass rather than a smarter import, because the skip guard an import uses is the
    /// survey's existence, not its completeness — and that is the right guard: it is what makes a
    /// half-finished import safe to resume. Weakening it so a re-run could notice missing photos
    /// would trade a guarantee for a convenience. This mode leaves answers and lifecycle alone and
    /// only fills in the media, which is the one thing arriving late can change.
    /// </summary>
    public const string BackfillMedia = "BACKFILL_MEDIA";

    public static readonly IReadOnlyList<string> All =
    [
        Validate,
        Import,
        BackfillMedia
    ];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
