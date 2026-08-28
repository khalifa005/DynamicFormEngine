using KH.Application.Fsms.Reporting.Common;
using KH.Application.Fsms.Reporting.Models;
using KH.Application.Fsms.Surveys.Queries.ExportSurveyPdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KH.Application.Fsms.Reporting.Queries.ExportSurveyTasksPdf;

/// <summary>
/// The PDF rendering of the Reports > Survey Tasks page. Every chart is rendered as a table of its
/// underlying data — see <c>GeneralStatisticsPdfDocument</c>'s remarks for why. The detail table can
/// run to thousands of rows (the export-rows query is capped at 20,000); QuestPDF paginates a
/// <c>Table</c> across pages natively and repeats <c>table.Header(...)</c> on each new page, so no
/// custom page-break handling is needed here.
/// </summary>
internal sealed class SurveyTasksPdfDocument : IDocument
{
    private const int LateTasksDisplayLimit = 20;

    private readonly SurveyTasksReportSummaryDto _summary;
    private readonly IReadOnlyList<SurveyTaskReportRowDto> _detailRows;
    private readonly IReadOnlyList<string> _appliedFilters;

    public SurveyTasksPdfDocument(
        SurveyTasksReportSummaryDto summary,
        IReadOnlyList<SurveyTaskReportRowDto> detailRows,
        IReadOnlyList<string> appliedFilters)
    {
        _summary = summary;
        _detailRows = detailRows;
        _appliedFilters = appliedFilters;
    }

    public void Compose(IDocumentContainer container)
    {
        SurveyPdfFonts.EnsureRegistered();

        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily(SurveyPdfFonts.LatinFamily, SurveyPdfFonts.ArabicFamily));

            page.Header().Element(c => ReportPdfHelpers.ComposeHeader(c, "Survey Tasks Report"));
            page.Content().Element(ComposeContent);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingTop(10).Column(column =>
        {
            column.Spacing(14);

            if (_appliedFilters.Count > 0)
            {
                column.Item().Element(c => ReportPdfHelpers.ComposeAppliedFilters(c, _appliedFilters));
            }

            column.Item().Element(ComposeKpis);
            column.Item().Element(ComposeStatusDistribution);
            column.Item().Element(ComposeTasksByTeam);
            column.Item().Element(ComposeTasksByBranch);
            column.Item().Element(ComposeLateTasks);
            column.Item().Element(ComposeDetailTable);
        });
    }

    private void ComposeKpis(IContainer container)
    {
        var kpis = _summary.Kpis;

        var rows = new List<(string Label, string Value)>
        {
            ("Total Tasks", kpis.TotalTasks.ToString()),
            ("Pending Tasks", kpis.PendingTasks.ToString()),
            ("In Progress Tasks", kpis.InProgressTasks.ToString()),
            ("Submitted Tasks", kpis.SubmittedTasks.ToString()),
            ("Approved Tasks", kpis.ApprovedTasks.ToString()),
            ("Returned Tasks", kpis.ReturnedTasks.ToString()),
            ("Overdue Tasks", kpis.OverdueTasks.ToString()),
            ("Completion Rate", ReportPdfHelpers.FormatPercent(kpis.CompletionRatePercent)),
        };

        ReportPdfHelpers.ComposeKpiTable(container, "KPI Summary", rows);
    }

    private void ComposeStatusDistribution(IContainer container) =>
        ReportPdfHelpers.ComposeTitledTable(container, "Status Distribution", _summary.StatusDistribution,
        [
            ("Status", r => r.Status),
            ("Count", r => r.Count.ToString()),
            ("Percent", r => ReportPdfHelpers.FormatPercent(r.Percent)),
        ]);

    private void ComposeTasksByTeam(IContainer container) =>
        ReportPdfHelpers.ComposeTitledTable(container, "Tasks by Team", _summary.TasksByTeam,
        [
            ("Team Name", r => r.TeamName),
            ("Count", r => r.Count.ToString()),
        ]);

    private void ComposeTasksByBranch(IContainer container) =>
        ReportPdfHelpers.ComposeTitledTable(container, "Tasks by Branch", _summary.TasksByBranch,
        [
            ("Branch Name", r => r.BranchName),
            ("Count", r => r.Count.ToString()),
        ]);

    private void ComposeLateTasks(IContainer container)
    {
        var lateTasks = _summary.LateTasks.Take(LateTasksDisplayLimit).ToList();

        ReportPdfHelpers.ComposeTitledTable(container, "Late Tasks", lateTasks,
        [
            ("Survey Code", r => r.SurveyCode),
            ("Status", r => r.Status),
            ("Days Late", r => r.DaysLate.ToString()),
            ("Due Date", r => ReportPdfHelpers.FormatDate(r.DueDate)),
            ("Team", r => ReportPdfHelpers.OrDash(r.TeamName)),
            ("Branch Code", r => ReportPdfHelpers.OrDash(r.BranchCode)),
        ]);
    }

    /// <summary>
    /// The full filtered row set, one row per survey task. A narrower column set than the Excel
    /// export's, since a PDF page is much narrower than a spreadsheet.
    /// </summary>
    private void ComposeDetailTable(IContainer container) =>
        ReportPdfHelpers.ComposeTitledTable(container, "Task Details", _detailRows,
        [
            ("Survey Code", r => r.SurveyCode),
            ("Status", r => r.Status),
            ("Source", r => r.Source),
            ("Branch Code", r => ReportPdfHelpers.OrDash(r.BranchCode)),
            ("Team", r => ReportPdfHelpers.OrDash(r.TeamName)),
            ("Created", r => ReportPdfHelpers.FormatDate(r.Created)),
            ("Due Date", r => ReportPdfHelpers.FormatDate(r.DueDate)),
            ("Submissions", r => r.SubmissionCount.ToString()),
        ]);
}
