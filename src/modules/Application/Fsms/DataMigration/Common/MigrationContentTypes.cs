namespace KH.Application.Fsms.DataMigration.Common;

/// <summary>
/// Content type for a file extension. An export names its media by id with no type recorded
/// anywhere in the data, so the extension on disk is the only thing there is to go on — and the
/// answer has to be right, because it is what the media viewer keys off when deciding whether to
/// render a photo or offer a download.
/// </summary>
public static class MigrationContentTypes
{
    /// <summary>What an unrecognised extension is stored as, matching <c>SubmissionFile</c>'s own default.</summary>
    public const string Fallback = "application/octet-stream";

    public const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".heic"] = "image/heic",
        [".mp4"] = "video/mp4",
        [".mov"] = "video/quicktime",
        [".m4a"] = "audio/mp4",
        [".mp3"] = "audio/mpeg",
        [".pdf"] = "application/pdf",
        [".csv"] = "text/csv",
        [".xlsx"] = Xlsx,
    };

    public static string For(string? extension) =>
        extension is not null && ByExtension.TryGetValue(extension, out var contentType)
            ? contentType
            : Fallback;
}
