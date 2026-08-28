using KH.Application.Fsms.Surveys.Models;
using KH.Domain.Entities.Fsms.Surveys;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;

namespace KH.Application.Fsms.Surveys.Queries.ExportSurveyPdf;

public class SurveyPdfDocument : IDocument
{
    private const string ArabicLanguage = "ar";
    private const string LogoResourceName = "KH.Application.Fsms.Surveys.Queries.ExportSurveyPdf.Resources.logo-sv.svg";
    private const float SignatureMaxWidth = 220f;
    private const float SignatureMaxHeight = 110f;
    private const float ImageMaxHeight = 260f;

    private readonly SurveyDetailDto _survey;
    private readonly IReadOnlyList<SurveyPdfFile> _files;
    // Temporarily omitted from the exported PDF.
    // private readonly IReadOnlyList<SurveyStatusHistory> _timeline;
    private readonly IReadOnlyList<(string Label, string Value)> _answers;
    private readonly string _language;

    public SurveyPdfDocument(
        SurveyDetailDto survey,
        IReadOnlyList<SurveyPdfFile> files,
        IReadOnlyList<SurveyStatusHistory> timeline,
        IReadOnlyList<(string Label, string Value)> answers,
        string language)
    {
        _survey = survey;
        _files = files;
        _ = timeline;
        // _timeline = timeline;
        _answers = answers;
        _language = language;
    }

    private bool IsArabic => _language == ArabicLanguage;

    public void Compose(IDocumentContainer container)
    {
        SurveyPdfFonts.EnsureRegistered();

        var isArabic = IsArabic;

        container
            .Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x =>
                {
                    var style = x.FontSize(10).FontFamily(SurveyPdfFonts.LatinFamily, SurveyPdfFonts.ArabicFamily);
                    if (isArabic)
                    {
                        style = style.DirectionFromRightToLeft();
                    }
                    return style;
                });

                if (isArabic)
                {
                    page.ContentFromRightToLeft();
                }

                var watermark = ReadLogoSvg();
                if (!string.IsNullOrEmpty(watermark))
                {
                    // Inject opacity to make it a subtle watermark
                    watermark = watermark.Replace("<svg ", "<svg opacity=\"0.05\" ");
                    page.Background().AlignCenter().AlignMiddle().Width(350).Svg(watermark);
                }

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
    }

    private static string ReadLogoSvg()
    {
        var assembly = typeof(SurveyPdfDocument).Assembly;
        using var stream = assembly.GetManifestResourceStream(LogoResourceName);
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void ComposeHeader(IContainer container)
    {
        var isArabic = IsArabic;
        var svgContent = ReadLogoSvg();

        container.PaddingBottom(15).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(isArabic ? "MK.FormEngine - تقرير الاستبيان" : "MK.FormEngine - Survey Report").FontSize(24).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().PaddingTop(5).Text(isArabic ? $"الرمز: {_survey.SurveyCode}" : $"Code: {_survey.SurveyCode}").FontSize(14).FontColor(Colors.Grey.Darken3);
                column.Item().Text(isArabic ? $"الحالة: {_survey.Status}" : $"Status: {_survey.Status}").FontSize(12).FontColor(Colors.Grey.Medium);
                column.Item().Text(isArabic ? $"تاريخ الإصدار: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC" : $"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(10).FontColor(Colors.Grey.Medium);
            });

            if (!string.IsNullOrEmpty(svgContent))
            {
                row.ConstantItem(150).AlignRight().AlignMiddle().Svg(svgContent);
            }
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(1, QuestPDF.Infrastructure.Unit.Centimetre).Column(column =>
        {
            column.Spacing(15);

            column.Item().Element(ComposeSurveyInfo);

            if (_answers.Any())
            {
                column.Item().Element(ComposeLastSubmittedData);
            }

            var signatures = _files.Where(f => f.IsSignature).ToList();
            if (signatures.Count > 0)
            {
                column.Item().Element(c => ComposeSignatures(c, signatures));
            }

            var images = _files.Where(f => f.IsImage && !f.IsSignature && f.HasBytes).ToList();
            if (images.Count > 0)
            {
                column.Item().Element(c => ComposeImages(c, images));
            }

            // Every uploaded file is listed, images included — a file whose bytes could not be read
            // must still appear, otherwise the report reads as if it was never uploaded.
            if (_files.Count > 0)
            {
                column.Item().Element(c => ComposeAttachedFiles(c, _files));
            }

            // Temporarily omitted from the exported PDF.
            // if (_timeline.Any())
            // {
            //     column.Item().Element(ComposeTimeline);
            // }
        });
    }

    private void ComposeSurveyInfo(IContainer container)
    {
        var isArabic = IsArabic;

        container.Column(column =>
        {
            column.Spacing(5);
            column.Item().Text(isArabic ? "معلومات الاستبيان" : "Survey Information").FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);

            column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(120);
                    columns.RelativeColumn();
                    columns.ConstantColumn(120);
                    columns.RelativeColumn();
                });

                void DrawRow(string label1, string value1, string label2, string value2, bool isAlternate)
                {
                    var bgColor = isAlternate ? Colors.Grey.Lighten4 : Colors.White;
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(label1).SemiBold();
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(value1);
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(label2).SemiBold();
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(value2);
                }

                DrawRow(isArabic ? "النموذج" : "Template", isArabic ? _survey.TemplateNameAr : _survey.TemplateNameEn, isArabic ? "رقم الأصل" : "FA ID", _survey.FaId ?? "-", false);
                DrawRow(isArabic ? "رمز المهمة" : "Task Code", _survey.TaskCode ?? "-", isArabic ? "تاريخ الاستحقاق" : "Due Date", _survey.DueDate?.ToString("yyyy-MM-dd") ?? "-", true);
                DrawRow(isArabic ? "المنطقة" : "Branch", isArabic ? _survey.BranchNameAr : _survey.BranchNameEn, isArabic ? "القسم" : "Department", isArabic ? _survey.DepartmentNameAr : _survey.DepartmentNameEn, false);
                DrawRow(isArabic ? "المصدر" : "Source", _survey.Source, isArabic ? "تاريخ الإنشاء" : "Created Date", _survey.Created.ToString("yyyy-MM-dd"), true);
            });
        });
    }

    private void ComposeLastSubmittedData(IContainer container)
    {
        var isArabic = IsArabic;
        container.Column(column =>
        {
            column.Spacing(5);
            column.Item().Text(isArabic ? "آخر البيانات المقدمة" : "Last Submitted Data").FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text(isArabic ? "الحقل" : "Field");
                    header.Cell().Element(HeaderCellStyle).Text(isArabic ? "القيمة" : "Value");
                });

                foreach (var answer in _answers)
                {
                    table.Cell().Element(ValueCellStyle).Text(answer.Label);
                    table.Cell().Element(ValueCellStyle).Text(answer.Value);
                }
            });
        });
    }

    /// <summary>The signed-off image, boxed like a signature line rather than dropped in the gallery.</summary>
    private void ComposeSignatures(IContainer container, IReadOnlyList<SurveyPdfFile> signatures)
    {
        var isArabic = IsArabic;
        container.Column(column =>
        {
            column.Spacing(10);
            column.Item().Text(isArabic ? "التوقيع" : "Signature").FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);

            foreach (var signature in signatures)
            {
                column.Item().Column(signatureColumn =>
                {
                    if (signature.HasBytes)
                    {
                        signatureColumn.Item()
                            .Border(1)
                            .BorderColor(Colors.Grey.Lighten1)
                            .Padding(8)
                            .MaxWidth(SignatureMaxWidth)
                            .MaxHeight(SignatureMaxHeight)
                            .Image(signature.Bytes!)
                            .FitArea();
                    }
                    else
                    {
                        signatureColumn.Item().Element(UnavailableNoteStyle).Text(UnavailableText);
                    }

                    signatureColumn.Item().PaddingTop(3).Text($"{FieldCaption(signature)}: {signature.FileName}").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            }
        });
    }

    private void ComposeImages(IContainer container, IReadOnlyList<SurveyPdfFile> images)
    {
        var isArabic = IsArabic;
        container.Column(column =>
        {
            column.Spacing(10);
            column.Item().Text(isArabic ? "الصور المرفقة" : "Attached Images").FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);

            foreach (var image in images)
            {
                column.Item().Column(imgCol =>
                {
                    imgCol.Item().MaxHeight(ImageMaxHeight).Image(image.Bytes!).FitArea();
                    imgCol.Item().Text($"{FieldCaption(image)}: {image.FileName}").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            }
        });
    }

    private void ComposeAttachedFiles(IContainer container, IReadOnlyList<SurveyPdfFile> files)
    {
        var isArabic = IsArabic;
        container.Column(column =>
        {
            column.Spacing(5);
            column.Item().Text(isArabic ? "الملفات المرفقة" : "Attached Files").FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(55);
                    columns.ConstantColumn(85);
                    columns.ConstantColumn(75);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text(isArabic ? "اسم الملف" : "File Name");
                    header.Cell().Element(HeaderCellStyle).Text(isArabic ? "الحقل" : "Field");
                    header.Cell().Element(HeaderCellStyle).Text(isArabic ? "النوع" : "Type");
                    header.Cell().Element(HeaderCellStyle).Text(isArabic ? "الحجم (ك.ب)" : "Size (KB)");
                    header.Cell().Element(HeaderCellStyle).Text(isArabic ? "تاريخ الرفع" : "Upload Date");
                    header.Cell().Element(HeaderCellStyle).Text(isArabic ? "الحالة" : "Status");
                });

                foreach (var file in files)
                {
                    table.Cell().Element(ValueCellStyle).Text(file.FileName);
                    table.Cell().Element(ValueCellStyle).Text(file.DataName);
                    table.Cell().Element(ValueCellStyle).Text(file.ContentType);
                    table.Cell().Element(ValueCellStyle).Text((file.SizeBytes / 1024).ToString());
                    table.Cell().Element(ValueCellStyle).Text(file.Created.ToString("yyyy-MM-dd HH:mm"));
                    table.Cell().Element(ValueCellStyle).Text(StatusText(file)).FontColor(file.IsImage && !file.HasBytes ? Colors.Red.Darken1 : Colors.Black);
                }
            });
        });
    }

    // Temporarily omitted from the exported PDF.
    // private void ComposeTimeline(IContainer container)
    // {
    //     var isArabic = IsArabic;
    //     container.Column(column =>
    //     {
    //         column.Spacing(5);
    //         column.Item().Text(isArabic ? "سجل الحالة" : "Status Timeline").FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);
    //
    //         column.Item().Table(table =>
    //         {
    //             table.ColumnsDefinition(columns =>
    //             {
    //                 columns.ConstantColumn(120);
    //                 columns.ConstantColumn(100);
    //                 columns.ConstantColumn(100);
    //                 columns.RelativeColumn();
    //             });
    //
    //             table.Header(header =>
    //             {
    //                 header.Cell().Element(HeaderCellStyle).Text(isArabic ? "التاريخ" : "Date");
    //                 header.Cell().Element(HeaderCellStyle).Text(isArabic ? "من" : "From");
    //                 header.Cell().Element(HeaderCellStyle).Text(isArabic ? "إلى" : "To");
    //                 header.Cell().Element(HeaderCellStyle).Text(isArabic ? "بواسطة" : "Changed By");
    //             });
    //
    //             foreach (var entry in _timeline)
    //             {
    //                 table.Cell().Element(ValueCellStyle).Text(entry.ChangedDate.ToString("yyyy-MM-dd HH:mm"));
    //                 table.Cell().Element(ValueCellStyle).Text(entry.FromStatus ?? "-");
    //                 table.Cell().Element(ValueCellStyle).Text(entry.ToStatus);
    //                 table.Cell().Element(ValueCellStyle).Text(entry.ChangedBy ?? "-");
    //             }
    //         });
    //     });
    // }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(x =>
        {
            x.Span("Page ");
            x.CurrentPageNumber();
            x.Span(" of ");
            x.TotalPages();
        });
    }

    private string FieldCaption(SurveyPdfFile file) => IsArabic ? $"الحقل: {file.DataName}" : $"Field: {file.DataName}";

    private string UnavailableText => IsArabic ? "الصورة غير متوفرة في التخزين" : "Image not available in storage";

    private string StatusText(SurveyPdfFile file) => file.IsImage
        ? (file.HasBytes
            ? (IsArabic ? "مضمّنة" : "Embedded")
            : (IsArabic ? "غير متوفرة" : "Unavailable"))
        : (IsArabic ? "مرفق" : "Attached");

    static IContainer HeaderCellStyle(IContainer container) => container.Background(Colors.Blue.Darken2).Padding(5).DefaultTextStyle(x => x.FontFamily(SurveyPdfFonts.LatinFamily, SurveyPdfFonts.ArabicFamily).FontColor(Colors.White).SemiBold());
    static IContainer ValueCellStyle(IContainer container) => container.Padding(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
    static IContainer UnavailableNoteStyle(IContainer container) => container.Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4).Padding(8).DefaultTextStyle(x => x.FontFamily(SurveyPdfFonts.LatinFamily, SurveyPdfFonts.ArabicFamily).FontColor(Colors.Red.Darken1).Italic());
}
