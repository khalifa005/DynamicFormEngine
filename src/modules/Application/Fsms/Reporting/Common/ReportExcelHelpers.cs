using ClosedXML.Excel;

namespace KH.Application.Fsms.Reporting.Common;

/// <summary>
/// Small ClosedXML building blocks shared by the three Excel-export handlers under
/// <c>Fsms/Reporting/Queries/Export*Excel</c> — a header-row style and the "Summary" sheet layout
/// every one of those reports uses, plus a couple of formatting conventions (dates, signed deltas)
/// so the three sheets read consistently. Deliberately thin: it exists to avoid three copies of
/// "write a bold header row" and "lay out a two-column summary sheet," not to become a generic
/// reporting framework.
/// </summary>
internal static class ReportExcelHelpers
{
    private const string HeaderFillColorHex = "#1F4E78";
    private const string DateFormat = "yyyy-MM-dd";
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm";
    private const string NotSetPlaceholder = "-";
    private const string NoFiltersPlaceholder = "(none — all data)";

    /// <summary>Writes a bold, white-on-fill header row at <paramref name="row"/> and freezes the pane above the data.</summary>
    internal static void AddHeaderRow(IXLWorksheet sheet, int row, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(HeaderFillColorHex);
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        sheet.SheetView.FreezeRows(row);
    }

    /// <summary>
    /// The consistent "Summary" sheet layout every report's export uses: title, generated-at
    /// timestamp, an "Applied Filters" section (only the filters the caller actually set), then a
    /// "KPIs" section as label/value rows.
    /// </summary>
    internal static void AddSummarySheet(
        IXLWorkbook workbook,
        string reportTitle,
        DateTimeOffset generatedAt,
        IReadOnlyList<(string Label, string Value)> filters,
        IReadOnlyList<(string Label, string Value)> kpis)
    {
        var sheet = workbook.Worksheets.Add("Summary");
        var row = 1;

        sheet.Cell(row, 1).Value = reportTitle;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Font.FontSize = 16;
        row += 2;

        sheet.Cell(row, 1).Value = "Generated At";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 2).Value = FormatDateTime(generatedAt);
        row += 2;

        sheet.Cell(row, 1).Value = "Applied Filters";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Font.FontSize = 12;
        row += 1;

        if (filters.Count == 0)
        {
            sheet.Cell(row, 1).Value = NoFiltersPlaceholder;
            row += 1;
        }
        else
        {
            foreach (var (label, value) in filters)
            {
                sheet.Cell(row, 1).Value = label;
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 2).Value = value;
                row += 1;
            }
        }

        row += 1;

        sheet.Cell(row, 1).Value = "KPIs";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Font.FontSize = 12;
        row += 1;

        foreach (var (label, value) in kpis)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 2).Value = value;
            row += 1;
        }

        sheet.Column(1).Width = 32;
        sheet.Column(2).Width = 30;
    }

    /// <summary>Appends <paramref name="label"/>/<paramref name="value"/> to the applied-filters list, skipping blank values — a filter the caller never set does not belong on the Summary sheet.</summary>
    internal static void AddFilter(List<(string Label, string Value)> filters, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add((label, value));
        }
    }

    /// <summary>Same as <see cref="AddFilter(List{ValueTuple{string,string}},string,string?)"/> for a date-range bound.</summary>
    internal static void AddFilter(List<(string Label, string Value)> filters, string label, DateTimeOffset? value)
    {
        if (value.HasValue)
        {
            filters.Add((label, FormatDate(value)));
        }
    }

    /// <summary>
    /// "+2.1" / "-0.4" / "0" — a delta is always rendered with an explicit sign (zero excepted) so a
    /// Summary sheet cell never reads as ambiguous about direction. Shared convention: append "%" at
    /// the call site, since not every delta in these reports is a percentage-point delta.
    /// </summary>
    internal static string FormatDelta(decimal delta) =>
        delta switch
        {
            > 0 => $"+{delta:0.##}",
            < 0 => delta.ToString("0.##"),
            _ => "0",
        };

    /// <summary><c>yyyy-MM-dd</c>, or "-" when unset — the one date format every export sheet uses.</summary>
    internal static string FormatDate(DateTimeOffset? value) =>
        value.HasValue ? value.Value.ToString(DateFormat) : NotSetPlaceholder;

    /// <summary><c>yyyy-MM-dd</c> for a non-nullable calendar date (e.g. a trend bucket's week start).</summary>
    internal static string FormatDate(DateOnly value) => value.ToString(DateFormat);

    /// <summary><c>yyyy-MM-dd</c>, or "-" when unset.</summary>
    internal static string FormatDate(DateOnly? value) =>
        value.HasValue ? value.Value.ToString(DateFormat) : NotSetPlaceholder;

    /// <summary><c>yyyy-MM-dd HH:mm</c> — used only for the Summary sheet's "Generated At" stamp.</summary>
    internal static string FormatDateTime(DateTimeOffset value) => value.ToString(DateTimeFormat);

    /// <summary>"-" for any other unset display value, kept as one shared constant rather than a literal repeated per handler.</summary>
    internal static string OrPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotSetPlaceholder : value;

    /// <summary>
    /// Auto-sizes every column to its content (ClosedXML's <c>AdjustToContents</c>). Safe for the
    /// small/bounded sheets (Summary, distributions, trends, late-work lists); the one sheet that can
    /// hold up to 20,000 rows (Survey Tasks' TaskDetails) must use <see cref="SetColumnWidths"/>
    /// instead — AdjustToContents is O(rows × cols) and gets slow past a few thousand rows.
    /// </summary>
    internal static void AutoFitColumns(IXLWorksheet sheet) => sheet.Columns().AdjustToContents();

    /// <summary>Fixed column widths for a large sheet where <see cref="AutoFitColumns"/> would be too slow.</summary>
    internal static void SetColumnWidths(IXLWorksheet sheet, params double[] widths)
    {
        for (var i = 0; i < widths.Length; i++)
        {
            sheet.Column(i + 1).Width = widths[i];
        }
    }
}
