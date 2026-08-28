namespace KH.Application.Fsms.Reporting.Models;

/// <summary>
/// The file every report export handler (Excel or PDF, any of the three reports) returns. Mirrors
/// <c>SurveyPdfResultDto</c>'s shape — content stream, content type, file name — but shared across
/// the six report export slices rather than redefined per slice, since all six hand the same three
/// things to the same controller pattern (<c>File(result.Data.Content, ...)</c>).
/// </summary>
public sealed record ReportExportResultDto(Stream Content, string ContentType, string FileName);

/// <summary>MIME types the report export handlers produce.</summary>
public abstract class ReportExportContentTypes
{
    public const string Excel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string Pdf = "application/pdf";
}
