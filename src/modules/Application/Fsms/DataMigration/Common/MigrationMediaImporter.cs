using System.Text.Json;
using KH.Application.Common.Interfaces;
using KH.Domain.Entities.Fsms.Submissions;

namespace KH.Application.Fsms.DataMigration.Common;

/// <summary>
/// Attaches an imported record's media to its survey and writes the answer that references it.
///
/// Nothing is copied. The archive was placed under the storage root in one go by whoever moved it
/// across; this finds each file by the id the source data carries and records a reference to it.
/// That is the only workable shape once an archive runs to a terabyte — pushing it through the
/// application would mean uploading it, copying it, and holding two of it.
///
/// The answer written is the array of <c>{ fileId, path, name, type, size }</c> that
/// <c>UploadSubmissionFileCommand</c> hands the browser — deliberately, not incidentally. Producing
/// anything else would mean the media viewer, the PDF export and <c>MediaFileReferences</c> each
/// needing a second code path for imported surveys; producing this one means an imported photo is
/// indistinguishable from an uploaded one everywhere downstream.
/// </summary>
internal static class MigrationMediaImporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>One media reference as it is written into the answer column.</summary>
    private sealed record FileReference(Guid FileId, string Path, string Name, string Type, long Size);

    /// <summary>
    /// Attaches every media key the record names, adding a <see cref="SubmissionFile"/> per file and
    /// writing the reference arrays into <paramref name="answers"/>. Left unsaved — the caller
    /// commits the record.
    /// </summary>
    /// <remarks>
    /// A key with no file behind it is counted, not thrown. An archive short a few photos still
    /// carries answers worth keeping, and the run report is where the operator learns how short it
    /// was.
    /// </remarks>
    public static async Task<MigrationMediaOutcome> ImportAsync(
        IApplicationDbContext context,
        IFileStorage fileStorage,
        MigrationArchive archive,
        MigrationRecord record,
        long templateId,
        long surveyId,
        Dictionary<string, object?> answers,
        CancellationToken cancellationToken)
    {
        var imported = 0;
        var missing = new List<string>();

        foreach (var (dataName, mediaKeys) in record.Media)
        {
            var references = new List<FileReference>(mediaKeys.Count);

            foreach (var mediaKey in mediaKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stored = await archive.FindAsync(fileStorage, mediaKey, cancellationToken);

                if (stored is null || stored.SizeBytes <= 0)
                {
                    missing.Add(mediaKey);
                    continue;
                }

                var fileId = Guid.NewGuid();
                var fileName = Path.GetFileName(stored.RelativePath);
                var contentType = MigrationContentTypes.For(Path.GetExtension(stored.RelativePath).ToLowerInvariant());

                context.SubmissionFiles.Add(SubmissionFile.CreateMigrated(
                    fileId, templateId, dataName, fileName, contentType, stored.SizeBytes, stored.RelativePath, surveyId));

                references.Add(new FileReference(fileId, stored.RelativePath, fileName, contentType, stored.SizeBytes));
                imported++;
            }

            if (references.Count > 0)
            {
                answers[dataName] = JsonSerializer.Serialize(references, SerializerOptions);
            }
        }

        return new MigrationMediaOutcome(imported, missing);
    }

    /// <summary>
    /// Attaches media that reached the archive <em>after</em> the record was imported, to a survey
    /// that already exists.
    ///
    /// Idempotent by construction: a media key whose file is already on the survey is reused rather
    /// than attached again, so running this twice attaches nothing the second time. The key is
    /// recovered from the stored file name — the archive is flat and a source id is the whole stem,
    /// so the name <em>is</em> the key.
    ///
    /// The answer it produces is the record's <em>full</em> reference array, old files and new
    /// together, in the order the source listed them. A partial array would silently drop the photos
    /// that were already there, so the caller must write this whole value or none of it.
    /// </summary>
    public static async Task<MigrationMediaBackfill> BackfillAsync(
        IApplicationDbContext context,
        IFileStorage fileStorage,
        MigrationArchive archive,
        MigrationRecord record,
        long templateId,
        long surveyId,
        long submissionId,
        IReadOnlyList<SubmissionFile> existingFiles,
        CancellationToken cancellationToken)
    {
        var attached = 0;
        var missing = new List<string>();
        var answers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (dataName, mediaKeys) in record.Media)
        {
            var onField = existingFiles
                .Where(file => string.Equals(file.DataName, dataName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var byKey = new Dictionary<string, SubmissionFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in onField)
            {
                byKey.TryAdd(Path.GetFileNameWithoutExtension(file.FileName), file);
            }

            var references = new List<FileReference>(mediaKeys.Count);
            var reused = new HashSet<long>();
            var attachedHere = 0;

            foreach (var mediaKey in mediaKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (byKey.TryGetValue(mediaKey, out var already))
                {
                    references.Add(ToReference(already));
                    reused.Add(already.Id);
                    continue;
                }

                var stored = await archive.FindAsync(fileStorage, mediaKey, cancellationToken);

                if (stored is null || stored.SizeBytes <= 0)
                {
                    missing.Add(mediaKey);
                    continue;
                }

                var fileId = Guid.NewGuid();
                var fileName = Path.GetFileName(stored.RelativePath);
                var contentType = MigrationContentTypes.For(Path.GetExtension(stored.RelativePath).ToLowerInvariant());

                var file = SubmissionFile.CreateMigrated(
                    fileId, templateId, dataName, fileName, contentType, stored.SizeBytes, stored.RelativePath, surveyId);

                // Linked immediately rather than left pending: there is no submit step coming that
                // would claim it. An empty path means "leave the bytes where they are", the same
                // no-move behaviour SubmissionMediaLinker relies on for migrated files.
                file.LinkTo(submissionId, string.Empty, surveyId);
                context.SubmissionFiles.Add(file);

                references.Add(new FileReference(fileId, stored.RelativePath, fileName, contentType, stored.SizeBytes));
                attached++;
                attachedHere++;
            }

            if (attachedHere == 0)
            {
                continue;
            }

            // Defensive: a file on the field that no current media key claims would otherwise be
            // dropped from the rewritten answer. It should not happen — the source column is the
            // only thing that ever created one — but losing a photo to rewrite it is not a trade
            // worth making.
            references.AddRange(onField.Where(file => !reused.Contains(file.Id)).Select(ToReference));

            answers[dataName] = JsonSerializer.Serialize(references, SerializerOptions);
        }

        return new MigrationMediaBackfill(attached, missing, answers);
    }

    private static FileReference ToReference(SubmissionFile file) =>
        new(file.FileId, file.RelativePath, file.FileName, file.ContentType, file.SizeBytes);

    /// <summary>
    /// Counts what an import <em>would</em> find without writing anything — the validate-only pass.
    /// Walks the same lookup, so an archive that has not been filled shows up here rather than after
    /// several hundred surveys have been written around it.
    /// </summary>
    public static async Task<MigrationMediaOutcome> ProbeAsync(
        IFileStorage fileStorage,
        MigrationArchive archive,
        MigrationRecord record,
        CancellationToken cancellationToken)
    {
        var found = 0;
        var missing = new List<string>();

        foreach (var mediaKey in record.Media.Values.SelectMany(keys => keys))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await archive.FindAsync(fileStorage, mediaKey, cancellationToken) is not null)
            {
                found++;
            }
            else
            {
                missing.Add(mediaKey);
            }
        }

        return new MigrationMediaOutcome(found, missing);
    }
}

/// <summary>
/// The one folder migrated media is read from, and the extensions worth trying against a bare id.
/// A value object rather than two loose strings so the job, the importer and the probe cannot end up
/// looking in different places.
/// </summary>
internal sealed record MigrationArchive(string Folder, IReadOnlyList<string> AllowedExtensions)
{
    public Task<StoredFile?> FindAsync(IFileStorage fileStorage, string mediaKey, CancellationToken cancellationToken) =>
        fileStorage.FindAsync(Folder, mediaKey, AllowedExtensions, cancellationToken);
}

/// <summary>What one record's media backfill amounted to.</summary>
/// <param name="Attached">Files newly added to the survey. Zero means there was nothing to do.</param>
/// <param name="MissingKeys">Keys still with no file behind them.</param>
/// <param name="Answers">
/// The rewritten media answers, keyed by <c>data_name</c> — only for the fields that gained a file,
/// and each carrying that field's whole reference array. Empty when nothing was attached.
/// </param>
internal sealed record MigrationMediaBackfill(
    int Attached,
    IReadOnlyList<string> MissingKeys,
    IReadOnlyDictionary<string, object?> Answers)
{
    public int MissingCount => MissingKeys.Count;

    public string? MissingMessage() => new MigrationMediaOutcome(Attached, MissingKeys).MissingMessage();
}

/// <summary>What one record's media amounted to.</summary>
/// <param name="Imported">Files attached (or, on a validate pass, found).</param>
/// <param name="MissingKeys">Keys with no file behind them.</param>
internal sealed record MigrationMediaOutcome(int Imported, IReadOnlyList<string> MissingKeys)
{
    /// <summary>Names the first few missing keys — enough to go looking, short enough to read.</summary>
    private const int MaxNamedKeys = 5;

    public int MissingCount => MissingKeys.Count;

    public string? MissingMessage()
    {
        if (MissingKeys.Count == 0)
        {
            return null;
        }

        var named = string.Join(", ", MissingKeys.Take(MaxNamedKeys));
        var suffix = MissingKeys.Count > MaxNamedKeys ? $" (+{MissingKeys.Count - MaxNamedKeys} more)" : string.Empty;

        return $"{MissingKeys.Count} media file(s) were not found in the archive: {named}{suffix}.";
    }
}
