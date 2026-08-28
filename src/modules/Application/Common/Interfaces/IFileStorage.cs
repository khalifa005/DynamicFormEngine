namespace KH.Application.Common.Interfaces;

/// <summary>Where a saved file landed and how big it turned out to be.</summary>
/// <param name="RelativePath">Storage-root-relative path — the only handle the app persists.</param>
/// <param name="SizeBytes">Bytes actually written.</param>
public sealed record StoredFile(string RelativePath, long SizeBytes);

/// <summary>A folder under the storage root, as an operator needs to see it.</summary>
/// <param name="FullPath">Absolute path, so the message names the folder to fill rather than describing it.</param>
/// <param name="Exists">Whether it is there yet.</param>
public sealed record StorageFolder(string FullPath, bool Exists);

/// <summary>
/// Binary file storage for uploaded survey media. Paths handed back are always relative to the
/// configured root, so swapping the local-disk implementation for blob/S3 later needs no change
/// to the rows that reference them.
/// </summary>
public interface IFileStorage
{
    /// <summary>Writes <paramref name="content"/> under <c>{subPath}/{fileName}</c> and returns its relative path.</summary>
    Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, string subPath, CancellationToken cancellationToken);

    /// <summary>Deletes the file at <paramref name="relativePath"/>; a missing file is not an error.</summary>
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken);

    /// <summary>Opens the stored file for reading. Throws <see cref="FileNotFoundException"/> when it is gone.</summary>
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken);

    /// <summary>Moves a file to a new location and returns its new relative path.</summary>
    Task<string> MoveAsync(string sourceRelativePath, string destinationSubPath, string fileName, CancellationToken cancellationToken);

    /// <summary>
    /// Looks for an already-present file under <paramref name="subPath"/> whose name is
    /// <paramref name="fileNameStem"/> plus one of <paramref name="extensions"/>, and returns the
    /// first that exists — or null when none does.
    ///
    /// Exists for media staged on the server rather than uploaded through the app: an export names
    /// its files by id and records no extension anywhere in its data, so the extension has to be
    /// discovered. Kept on this interface rather than done with <c>File.Exists</c> at the call site
    /// so that root resolution — and the check that a crafted name cannot escape it — stays in the
    /// one implementation that owns the storage root.
    /// </summary>
    Task<StoredFile?> FindAsync(string subPath, string fileNameStem, IReadOnlyList<string> extensions, CancellationToken cancellationToken);

    /// <summary>
    /// Where a folder under the root actually is, and whether it exists. Exists so an admin screen
    /// can tell someone the exact path to drop an archive into — guessing at that is precisely how
    /// an import ends up finding nothing.
    /// </summary>
    StorageFolder Describe(string subPath);
}
