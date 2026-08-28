using KH.Application.Fsms.Surveys.Queries.ExportSurveyPdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KH.Application.Fsms.Reporting.Common;

/// <summary>
/// Shared QuestPDF building blocks for the three report-export documents
/// (<c>GeneralStatisticsPdfDocument</c>, <c>SurveyTasksPdfDocument</c>, <c>TeamPerformancePdfDocument</c>).
/// English-only, no watermark — unlike <c>SurveyPdfDocument</c>, these reports do not need RTL/Arabic
/// support, so this stays a thin set of helpers rather than a second copy of that document's page
/// setup and font handling. Font registration itself is still delegated to
/// <see cref="SurveyPdfFonts.EnsureRegistered"/> — one process-wide registration, not a duplicate.
/// </summary>
internal static class ReportPdfHelpers
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm";
    private const string NoDataText = "No data available.";

    /// <summary>Report title, generated-at timestamp, and a "Page x of y" indicator — repeats on every page.</summary>
    public static void ComposeHeader(IContainer container, string title)
    {
        container.PaddingBottom(12).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(title).FontSize(18).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().PaddingTop(3).Text($"Generated: {DateTimeOffset.UtcNow.ToString(DateTimeFormat)} UTC").FontSize(9).FontColor(Colors.Grey.Medium);
            });

            row.ConstantItem(110).AlignRight().AlignMiddle().Text(x =>
            {
                x.DefaultTextStyle(y => y.FontSize(9).FontColor(Colors.Grey.Medium));
                x.Span("Page ");
                x.CurrentPageNumber();
                x.Span(" of ");
                x.TotalPages();
            });
        });
    }

    /// <summary>One line per filter the caller actually set. Renders nothing when the list is empty.</summary>
    public static void ComposeAppliedFilters(IContainer container, IReadOnlyList<string> filterLines)
    {
        if (filterLines.Count == 0)
        {
            return;
        }

        container.Column(column =>
        {
            column.Spacing(2);
            column.Item().Text("Applied Filters").FontSize(12).SemiBold().FontColor(Colors.Blue.Darken2);

            foreach (var line in filterLines)
            {
                column.Item().Text(line).FontSize(9).FontColor(Colors.Grey.Darken2);
            }
        });
    }

    /// <summary>A titled two-column label/value table — used for every report's KPI summary block.</summary>
    public static void ComposeKpiTable(IContainer container, string title, IReadOnlyList<(string Label, string Value)> rows)
    {
        ComposeTitledTable(container, title, rows, [
            ("Metric", r => r.Label),
            ("Value", r => r.Value),
        ]);
    }

    /// <summary>
    /// A titled table with a bold header row and alternating row shading, built from arbitrary column
    /// header + cell-value-selector pairs so each report document does not hand-roll
    /// <c>ColumnsDefinition</c>/<c>Header</c>/<c>Cell</c> boilerplate per table. QuestPDF repeats the
    /// header row automatically when the table spans multiple pages, which is what lets the (possibly
    /// large) Survey Tasks detail table paginate natively instead of needing custom page-break logic.
    /// </summary>
    public static void ComposeTitledTable<TRow>(
        IContainer container,
        string title,
        IReadOnlyList<TRow> rows,
        IReadOnlyList<(string Header, Func<TRow, string> ValueSelector)> columns)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item().Text(title).FontSize(12).SemiBold().FontColor(Colors.Blue.Darken2);

            if (rows.Count == 0)
            {
                column.Item().Text(NoDataText).FontSize(9).Italic().FontColor(Colors.Grey.Medium);
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    foreach (var _ in columns)
                    {
                        cols.RelativeColumn();
                    }
                });

                table.Header(header =>
                {
                    foreach (var col in columns)
                    {
                        header.Cell().Element(HeaderCellStyle).Text(col.Header);
                    }
                });

                var rowIndex = 0;
                foreach (var row in rows)
                {
                    var isAlternate = rowIndex % 2 == 1;
                    foreach (var col in columns)
                    {
                        table.Cell().Element(c => ValueCellStyle(c, isAlternate)).Text(col.ValueSelector(row) ?? "-");
                    }

                    rowIndex++;
                }
            });
        });
    }

    /// <summary>"+4.2%" / "-1.1%" — the sign is always shown so a delta reads as a change, not a raw number.</summary>
    public static string FormatDelta(decimal delta) =>
        $"{(delta >= 0 ? "+" : string.Empty)}{delta.ToString("0.0")}%";

    public static string FormatPercent(decimal value) => $"{value.ToString("0.0")}%";

    public static string FormatDate(DateOnly? value) => value?.ToString(DateFormat) ?? "-";

    public static string FormatDate(DateTimeOffset? value) => value?.ToString(DateFormat) ?? "-";

    public static string FormatDateTime(DateTimeOffset? value) => value?.ToString(DateTimeFormat) ?? "-";

    public static string OrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value;

    /// <summary>Adds "Label: value" when <paramref name="value"/> is set — the "skip nulls" rule for applied-filter lines.</summary>
    public static void AddFilterIfPresent(List<string> lines, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{label}: {value}");
        }
    }

    public static void AddFilterIfPresent(List<string> lines, string label, long? value)
    {
        if (value.HasValue)
        {
            lines.Add($"{label}: {value.Value}");
        }
    }

    /// <summary>"Date range: 2026-08-01 to 2026-08-20" (or a one-sided "From"/"To" line) when either bound is set.</summary>
    public static void AddDateRangeFilterIfPresent(List<string> lines, DateTimeOffset? fromDate, DateTimeOffset? toDate)
    {
        if (fromDate.HasValue && toDate.HasValue)
        {
            lines.Add($"Date range: {fromDate.Value.ToString(DateFormat)} to {toDate.Value.ToString(DateFormat)}");
        }
        else if (fromDate.HasValue)
        {
            lines.Add($"From: {fromDate.Value.ToString(DateFormat)}");
        }
        else if (toDate.HasValue)
        {
            lines.Add($"To: {toDate.Value.ToString(DateFormat)}");
        }
    }

    private static IContainer HeaderCellStyle(IContainer container) =>
        container.Background(Colors.Blue.Darken2).Padding(5).DefaultTextStyle(x =>
            x.FontFamily(SurveyPdfFonts.LatinFamily, SurveyPdfFonts.ArabicFamily).FontColor(Colors.White).SemiBold().FontSize(9));

    private static IContainer ValueCellStyle(IContainer container, bool isAlternate) =>
        container
            .Background(isAlternate ? Colors.Grey.Lighten4 : Colors.White)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(5)
            .DefaultTextStyle(x => x.FontFamily(SurveyPdfFonts.LatinFamily, SurveyPdfFonts.ArabicFamily).FontSize(9));
}
