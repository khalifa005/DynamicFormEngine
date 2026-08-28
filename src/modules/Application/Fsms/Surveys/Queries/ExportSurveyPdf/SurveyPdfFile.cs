using KH.Application.Fsms.Common.Definition;

namespace KH.Application.Fsms.Surveys.Queries.ExportSurveyPdf;

/// <summary>
/// One uploaded file as the report sees it. <see cref="Bytes"/> is only read for images, and stays
/// null when the row outlived its bytes in storage — the report still lists the file as unavailable
/// rather than dropping it silently.
/// </summary>
public sealed record SurveyPdfFile(
    Guid FileId,
    string DataName,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset Created,
    string? FieldType,
    byte[]? Bytes)
{
    public const string ImageContentTypePrefix = "image/";

    public bool IsImage => ContentType.StartsWith(ImageContentTypePrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>A signature is an image too, but it gets its own section rather than the gallery.</summary>
    public bool IsSignature => string.Equals(FieldType, BuilderElementTypes.Signature, StringComparison.OrdinalIgnoreCase);

    public bool HasBytes => Bytes is { Length: > 0 };
}
