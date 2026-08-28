using KH.Application.Fsms.Reporting.Common;
using KH.Application.Fsms.Reporting.Models;
using KH.Application.Fsms.Surveys.Queries.ExportSurveyPdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KH.Application.Fsms.Reporting.Queries.ExportGeneralStatisticsPdf;

/// <summary>
/// The PDF rendering of the Reports > General Statistics page: applied filters, KPI summary, status
/// distribution, team completion, late tasks and late teams, each rendered as a table of its
/// underlying data rather than a drawn chart — QuestPDF has no built-in charting and a custom
/// chart-drawing layer is out of scope for this pass, per the reporting spec's guidance not to force
/// chart recreation into an export. The weekly trend and recent-activity widgets from the on-screen
/// report are not part of this export's spec and are intentionally omitted.
/// </summary>
internal sealed class GeneralStatisticsPdfDocument : IDocument
{
    private const int LateTasksDisplayLimit = 20;

    private readonly GeneralStatisticsReportDto _report;
    private readonly IReadOnlyList<string> _appliedFilters;

    public GeneralStatisticsPdfDocument(GeneralStatisticsReportDto report, IReadOnlyList<string> appliedFilters)
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

            page.Header().Element(c => ReportPdfHelpers.ComposeHeader(c, "General Statistics Report"));
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
            column.Item().Element(ComposeTeamCompletion);
            column.Item().Element(ComposeLateTasks);
            column.Item().Element(ComposeLateTeams);
        });
    }

    private void ComposeKpis(IContainer container)
    {
        var kpis = _report.Kpis;

        var rows = new List<(string Label, string Value)>
        {
            ("Total Tasks", $"{kpis.TotalTasks} ({ReportPdfHelpers.FormatDelta(kpis.TotalTasksDelta)})"),
            ("Overdue Tasks", $"{kpis.OverdueTasks} ({ReportPdfHelpers.FormatDelta(kpis.OverdueTasksDelta)})"),
            ("Total Teams", kpis.TotalTeams.ToString()),
            ("Active Teams", $"{kpis.ActiveTeams} ({ReportPdfHelpers.FormatDelta(kpis.ActiveTeamsDelta)})"),
            ("Avg Completion Hours", kpis.AvgCompletionHours.ToString("0.0")),
            ("Completion Rate", $"{ReportPdfHelpers.FormatPercent(kpis.CompletionRatePercent)} ({ReportPdfHelpers.FormatDelta(kpis.CompletionRateDelta)})"),
            ("Return Rate", $"{ReportPdfHelpers.FormatPercent(kpis.ReturnRatePercent)} ({ReportPdfHelpers.FormatDelta(kpis.ReturnRateDelta)})"),
            ("On-Time Rate", $"{ReportPdfHelpers.FormatPercent(kpis.OnTimeRatePercent)} ({ReportPdfHelpers.FormatDelta(kpis.OnTimeRateDelta)})"),
        };

        ReportPdfHelpers.ComposeKpiTable(container, "KPI Summary", rows);
    }

    private void ComposeStatusDistribution(IContainer container) =>
        ReportPdfHelpers.ComposeTitledTable(container, "Status Distribution", _report.StatusDistribution,
        [
            ("Status", r => r.Status),
            ("Count", r => r.Count.ToString()),
            ("Percent", r => ReportPdfHelpers.FormatPercent(r.Percent)),
        ]);

    private void ComposeTeamCompletion(IContainer container) =>
        ReportPdfHelpers.ComposeTitledTable(container, "Team Completion", _report.TeamCompletion,
        [
            ("Team Name", r => r.TeamName),
            ("Assigned", r => r.Assigned.ToString()),
            ("Completed", r => r.Completed.ToString()),
            ("Completion %", r => ReportPdfHelpers.FormatPercent(r.CompletionPercent)),
        ]);

    private void ComposeLateTasks(IContainer container)
    {
        var lateTasks = _report.LateTasks.Take(LateTasksDisplayLimit).ToList();

        ReportPdfHelpers.ComposeTitledTable(container, "Late Tasks", lateTasks,
        [
            ("Survey Code", r => r.SurveyCode),
            ("Status", r => r.Status),
            ("Days Late", r => r.DaysLate.ToString()),
            ("Due Date", r => ReportPdfHelpers.FormatDate(r.DueDate)),
            ("Field Team", r => ReportPdfHelpers.OrDash(r.FieldTeamName)),
            ("Branch Code", r => ReportPdfHelpers.OrDash(r.BranchCode)),
        ]);
    }

    private void ComposeLateTeams(IContainer container) =>
        ReportPdfHelpers.ComposeTitledTable(container, "Late Teams", _report.LateTeams,
        [
            ("Field Team", r => ReportPdfHelpers.OrDash(r.FieldTeamName)),
            ("Survey Count", r => r.SurveyCount.ToString()),
            ("Overdue Count", r => r.OverdueCount.ToString()),
            ("On-Time Rate", r => ReportPdfHelpers.FormatPercent(r.OnTimeRatePercent)),
        ]);
}
