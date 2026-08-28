using System.Text.Json;
using KH.Application.Common.Interfaces;
using KH.Application.Fsms.Common.Definition;
using KH.Domain.Constants.Fsms;

namespace KH.Application.Fsms.Submissions.Common;

/// <summary>
/// Claims the media files a submission referenced. Shared by the single and bulk submit slices —
/// the rule about which files a submission may claim is a security boundary, and one copy of it is
/// easier to keep right than two.
/// </summary>
internal static class SubmissionMediaLinker
{
    /// <summary>
    /// Links the files this submission's answers name. Only files uploaded against the same template
    /// and still <c>PENDING</c> can be claimed, so a fabricated file id cannot steal a file belonging
    /// to another submission. Left unsaved — the caller commits once.
    ///
    /// <paramref name="surveyId"/> backfills the survey onto any file that lacked it — the case when
    /// the crew raised the survey after uploading its media, so the survey did not exist at upload
    /// time.
    /// </summary>
    public static async Task LinkAsync(
        IApplicationDbContext context,
        IFileStorage fileStorage,
        SurveyDefinition definition,
        IReadOnlyDictionary<string, object?> answers,
        long templateId,
        long submissionId,
        long surveyId,
        string surveyCode,
        CancellationToken cancellationToken)
    {
        var fileIds = MediaFileReferences.Extract(definition, answers);
        if (fileIds.Count == 0)
        {
            return;
        }

        var files = await context.SubmissionFiles
            .Where(f => fileIds.Contains(f.FileId)
                && f.TemplateId == templateId
                && f.Status == SubmissionFileStatuses.Pending)
            .ToListAsync(cancellationToken);

        if (files.Count == 0)
        {
            return;
        }

        foreach (var file in files)
        {
            // A migrated file belongs to the archive, referenced where it already sits. Filing it
            // under the survey would relocate an archive this application does not own — and at
            // archive scale that is a terabyte of pointless churn, on files two records may share.
            // Passing no new path leaves it exactly where it is; LinkTo ignores an empty one.
            var newPath = file.IsMigrated
                ? string.Empty
                : await fileStorage.MoveAsync(file.RelativePath, surveyCode, file.FileName, cancellationToken);

            file.LinkTo(submissionId, newPath, surveyId);
        }
    }
}

/// <summary>
/// Pulls the <c>fileId</c>s out of the media answers. A media field's value is a JSON array of
/// <c>{ fileId, path, name, type, size }</c> references — the bytes live in storage, never here.
/// </summary>
internal static class MediaFileReferences
{
    private const string FileIdProperty = "fileId";

    public static IReadOnlyCollection<Guid> Extract(SurveyDefinition definition, IReadOnlyDictionary<string, object?> answers)
    {
        var mediaNames = definition.Fields
            .Where(f => BuilderElementTypes.IsMedia(f.FieldType))
            .Select(f => f.DataName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (mediaNames.Count == 0)
        {
            return [];
        }

        var fileIds = new HashSet<Guid>();

        foreach (var (key, value) in answers)
        {
            // `mediaNames` comes from the parsed definition, whose data_names are trimmed; the
            // caller has already put the answers on those same keys (see SurveyAnswerKeys).
            if (!mediaNames.Contains(key))
            {
                continue;
            }

            CollectFrom(value, fileIds);
        }

        return fileIds;
    }

    private static void CollectFrom(object? value, HashSet<Guid> sink)
    {
        switch (value)
        {
            case JsonElement json:
                CollectFromJson(json, sink);
                return;

            // Media answers (including signature) are a JSON array of file refs. A legacy
            // data-URL string is normalised before LinkAsync runs, so it never reaches here.
            case string text when text.TrimStart().StartsWith('['):
                try
                {
                    using var document = JsonDocument.Parse(text);
                    CollectFromJson(document.RootElement, sink);
                }
                catch (JsonException)
                {
                    // Not a reference array; nothing to link.
                }

                return;
        }
    }

    private static void CollectFromJson(JsonElement element, HashSet<Guid> sink)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty(FileIdProperty, out var fileId)
                || fileId.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (Guid.TryParse(fileId.GetString(), out var parsed))
            {
                sink.Add(parsed);
            }
        }
    }
}
