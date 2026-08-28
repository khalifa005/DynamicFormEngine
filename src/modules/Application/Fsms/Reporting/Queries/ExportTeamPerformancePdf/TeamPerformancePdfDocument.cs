using KH.Application.Fsms.Reporting.Common;
using KH.Application.Fsms.Reporting.Models;
using KH.Application.Fsms.Surveys.Queries.ExportSurveyPdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KH.Application.Fsms.Reporting.Queries.ExportTeamPerformancePdf;

/// <summary>
/// The PDF rendering of the Reports > Team Performance page. Every chart is rendered as a table of
/// its underlying data — see <c>GeneralStatisticsPdfDocument</c>'s remarks for why.
/// </summary>
internal sealed class TeamPerformancePdfDocument : IDocument
{
    private readonly TeamPerformanceReportDto _report;
    private readonly IReadOnlyList<string> _appliedFilters;

    public TeamPerformancePdfDocument(TeamPerformanceReportDto report, IReadOnlyList<string> appliedFilters)
    {
        _report = report;
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

            page.Header().Element(c => ReportPdfHelpers.ComposeHeader(c, "Team Performance Report"));
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
            column.Item().Element(ComposeTeamPerformance);
            column.Item().Element(ComposeWeeklyCompletionTrend);
            column.Item().Element(ComposeProductivityTrend);
        });
    }

    private void ComposeKpis(IContainer container)
    {
        var kpis = _report.Kpis;

        var rows = new List<(string Label, string Value)>
        {
            ("Assigned Tasks", kpis.AssignedTasks.ToString()),
            ("Completed Tasks", kpis.CompletedTasks.ToString()),
            ("Pending Tasks", kpis.PendingTasks.ToString()),
            ("Overdue Tasks", kpis.OverdueTasks.ToString()),
            ("Completion Rate", ReportPdfHelpers.FormatPercent(kpis.CompletionRatePercent)),
            ("Avg Completion Hours", kpis.AvgCompletionHours.ToString("0.0")),
        };

        ReportPdfHelpers.ComposeKpiTable(container, "KPI Summary", rows);
    }

    /// <summary>Every <c>TeamPerformanceRowDto</c> column — the report's core per-team breakdown.</summary>
    private void ComposeTeamPerformance(IContainer container) =>
        ReportPdfHelpers.ComposeTitledTable(container, "Team Performance", _report.Teams,
        [
            ("Team Name", r => r.TeamName),
            ("Branch Code", r => ReportPdfHelpers.OrDash(r.BranchCode)),
            ("Assigned", r => r.Assigned.ToString()),
            ("Completed", r => r.Completed.ToString()),
            ("Pending", r => r.Pending.ToString()),
            ("In Progress", r => r.InProgress.ToString()),
            ("Returned", r => r.Returned.ToString()),
            ("Completion %", r => ReportPdfHelpers.FormatPercent(r.CompletionPercent)),
            ("Avg Completion Hrs", r => r.AvgCompletionHours.ToString("0.0")),
            ("Daily Productivity", r => r.DailyProductivity.ToString("0.00")),
            ("Overdue", r => r.OverdueCount.ToString()),
            ("On-Time Rate", r => ReportPdfHelpers.FormatPercent(r.OnTimeRatePercent)),
        ]);

    private void ComposeWeeklyCompletionTrend(IContainer container) =>
        ReportPdfHelpers.ComposeTitledTable(container, "Weekly Completion Trend", _report.WeeklyCompletionTrend,
        [
            ("Team Name", r => r.TeamName),
            ("Week Start", r => ReportPdfHelpers.FormatDate(r.WeekStart)),
            ("Completed Count", r => r.CompletedCount.ToString()),
        ]);

    private void ComposeProductivityTrend(IContainer container) =>
        ReportPdfHelpers.ComposeTitledTable(container, "Productivity Trend", _report.ProductivityTrend,
        [
            ("Week Start", r => ReportPdfHelpers.FormatDate(r.WeekStart)),
            ("Avg Completed per Team", r => r.AvgCompletedPerTeam.ToString("0.00")),
        ]);
}
