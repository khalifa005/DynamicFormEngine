using ClosedXML.Excel;
using KH.Application.Common.Security;
using KH.Application.Fsms.Reporting.Common;
using KH.Application.Fsms.Reporting.Models;
using KH.Application.Fsms.Reporting.Queries.GetGeneralStatisticsReport;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Reporting.Queries.ExportGeneralStatisticsExcel;

/// <summary>
/// Excel export for the Reports &gt; General Statistics page. Delegates every number to
/// <see cref="GetGeneralStatisticsReportQuery"/> — the same query the screen itself reads — and only
/// re-shapes the result into a workbook, so the export can never drift from what the user sees.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewReports)]
public record ExportGeneralStatisticsExcelQuery : IRequest<Result<ReportExportResultDto>>
{
    /// <summary>Inclusive lower bound on survey creation.</summary>
    public DateTimeOffset? FromDate { get; init; }

    /// <summary>Inclusive upper bound on survey creation.</summary>
    public DateTimeOffset? ToDate { get; init; }

    public string? ClusterCode { get; init; }
    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public string? Status { get; init; }
    public string? Source { get; init; }
}

public sealed class ExportGeneralStatisticsExcelQueryValidator : AbstractValidator<ExportGeneralStatisticsExcelQuery>
{
    public ExportGeneralStatisticsExcelQueryValidator()
    {
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value)
            .WithMessage("The end of the date range must not precede its start.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);

        RuleFor(x => x.Status)
            .Must(SurveyStatuses.IsDefined).WithMessage("Status is not a recognized survey status.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));

        RuleFor(x => x.Source)
            .Must(SurveySources.IsDefined).WithMessage("Source is not a recognized survey source.")
            .When(x => !string.IsNullOrWhiteSpace(x.Source));
    }
}

public sealed class ExportGeneralStatisticsExcelQueryHandler(ISender sender)
    : IRequestHandler<ExportGeneralStatisticsExcelQuery, Result<ReportExportResultDto>>
{
    private const string ReportTitle = "General Statistics Report";

    public async Task<Result<ReportExportResultDto>> Handle(
        ExportGeneralStatisticsExcelQuery request, CancellationToken cancellationToken)
    {
        var reportResult = await sender.Send(
            new GetGeneralStatisticsReportQuery
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                ClusterCode = request.ClusterCode,
                CbuCode = request.CbuCode,
                BranchCode = request.BranchCode,
                OperationAreaCode = request.OperationAreaCode,
                Status = request.Status,
                Source = request.Source,
            },
            cancellationToken);

        if (!reportResult.IsSuccess || reportResult.Data is null)
        {
            return Result<ReportExportResultDto>.Fail(reportResult.Errors);
        }

        var report = reportResult.Data;
        var generatedAt = DateTimeOffset.UtcNow;

        using var workbook = new XLWorkbook();

        ReportExcelHelpers.AddSummarySheet(
            workbook, ReportTitle, generatedAt, BuildAppliedFilters(request), BuildKpiRows(report.Kpis));

        AddStatusDistributionSheet(workbook, report.StatusDistribution);
        AddWeeklyTrendSheet(workbook, report.Trend);
        AddTeamCompletionSheet(workbook, report.TeamCompletion);
        AddLateTasksSheet(workbook, report.LateTasks);
        AddLateTeamsSheet(workbook, report.LateTeams);

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"GeneralStatistics_{generatedAt:yyyyMMdd_HHmmss}.xlsx";

        return Result<ReportExportResultDto>.Success(
            new ReportExportResultDto(stream, ReportExportContentTypes.Excel, fileName));
    }

    private static List<(string Label, string Value)> BuildAppliedFilters(ExportGeneralStatisticsExcelQuery request)
    {
        var filters = new List<(string Label, string Value)>();

        ReportExcelHelpers.AddFilter(filters, "From Date", request.FromDate);
        ReportExcelHelpers.AddFilter(filters, "To Date", request.ToDate);
        ReportExcelHelpers.AddFilter(filters, "Cluster", request.ClusterCode);
        ReportExcelHelpers.AddFilter(filters, "CBU", request.CbuCode);
        ReportExcelHelpers.AddFilter(filters, "Branch", request.BranchCode);
        ReportExcelHelpers.AddFilter(filters, "Operation Area", request.OperationAreaCode);
        ReportExcelHelpers.AddFilter(filters, "Status", request.Status);
        ReportExcelHelpers.AddFilter(filters, "Source", request.Source);

        return filters;
    }

    private static List<(string Label, string Value)> BuildKpiRows(GeneralStatisticsKpisDto kpis) =>
    [
        ("Total Tasks", $"{kpis.TotalTasks} ({ReportExcelHelpers.FormatDelta(kpis.TotalTasksDelta)}%)"),
        ("Overdue Tasks", $"{kpis.OverdueTasks} ({ReportExcelHelpers.FormatDelta(kpis.OverdueTasksDelta)}%)"),
        ("Total Teams", kpis.TotalTeams.ToString()),
        ("Active Teams", $"{kpis.ActiveTeams} ({ReportExcelHelpers.FormatDelta(kpis.ActiveTeamsDelta)}%)"),
        ("Avg Completion Hours", kpis.AvgCompletionHours.ToString("0.##")),
        ("Completion Rate", $"{kpis.CompletionRatePercent:0.##}% ({ReportExcelHelpers.FormatDelta(kpis.CompletionRateDelta)}%)"),
        ("Return Rate", $"{kpis.ReturnRatePercent:0.##}% ({ReportExcelHelpers.FormatDelta(kpis.ReturnRateDelta)}%)"),
        ("On-Time Rate", $"{kpis.OnTimeRatePercent:0.##}% ({ReportExcelHelpers.FormatDelta(kpis.OnTimeRateDelta)}%)"),
    ];

    private static void AddStatusDistributionSheet(
        XLWorkbook workbook, IReadOnlyList<DashboardStatusSliceDto> statusDistribution)
    {
        var sheet = workbook.Worksheets.Add("StatusDistribution");
        ReportExcelHelpers.AddHeaderRow(sheet, 1, "Status", "Count", "Percent");

        var row = 2;
        foreach (var item in statusDistribution)
        {
            sheet.Cell(row, 1).Value = item.Status;
            sheet.Cell(row, 2).Value = item.Count;
            sheet.Cell(row, 3).Value = item.Percent;
            row++;
        }

        ReportExcelHelpers.AutoFitColumns(sheet);
    }

    private static void AddWeeklyTrendSheet(XLWorkbook workbook, IReadOnlyList<DashboardTrendPointDto> trend)
    {
        var sheet = workbook.Worksheets.Add("WeeklyTrend");
        ReportExcelHelpers.AddHeaderRow(
            sheet, 1, "Bucket", "Created", "Assigned", "Submitted", "Approved", "Returned");

        var row = 2;
        foreach (var point in trend)
        {
            sheet.Cell(row, 1).Value = point.BucketKey;
            sheet.Cell(row, 2).Value = point.CreatedCount;
            sheet.Cell(row, 3).Value = point.AssignedCount;
            sheet.Cell(row, 4).Value = point.SubmittedCount;
            sheet.Cell(row, 5).Value = point.ApprovedCount;
            sheet.Cell(row, 6).Value = point.ReturnedCount;
            row++;
        }

        ReportExcelHelpers.AutoFitColumns(sheet);
    }

    private static void AddTeamCompletionSheet(
        XLWorkbook workbook, IReadOnlyList<GeneralStatisticsTeamCompletionDto> teamCompletion)
    {
        var sheet = workbook.Worksheets.Add("TeamCompletion");
        ReportExcelHelpers.AddHeaderRow(sheet, 1, "Team Name", "Assigned", "Completed", "Completion %");

        var row = 2;
        foreach (var team in teamCompletion)
        {
            sheet.Cell(row, 1).Value = team.TeamName;
            sheet.Cell(row, 2).Value = team.Assigned;
            sheet.Cell(row, 3).Value = team.Completed;
            sheet.Cell(row, 4).Value = team.CompletionPercent;
            row++;
        }

        ReportExcelHelpers.AutoFitColumns(sheet);
    }

    private static void AddLateTasksSheet(XLWorkbook workbook, IReadOnlyList<DashboardLateSurveyDto> lateTasks)
    {
        var sheet = workbook.Worksheets.Add("LateTasks");
        ReportExcelHelpers.AddHeaderRow(
            sheet, 1, "Survey Code", "Status", "Lateness Kind", "Days Late", "Due Date", "Team Name", "Branch Code", "Template");

        var row = 2;
        foreach (var task in lateTasks)
        {
            sheet.Cell(row, 1).Value = task.SurveyCode;
            sheet.Cell(row, 2).Value = task.Status;
            sheet.Cell(row, 3).Value = task.LatenessKind;
            sheet.Cell(row, 4).Value = task.DaysLate;
            sheet.Cell(row, 5).Value = ReportExcelHelpers.FormatDate(task.DueDate);
            sheet.Cell(row, 6).Value = ReportExcelHelpers.OrPlaceholder(task.FieldTeamName);
            sheet.Cell(row, 7).Value = ReportExcelHelpers.OrPlaceholder(task.BranchCode);
            sheet.Cell(row, 8).Value = ReportExcelHelpers.OrPlaceholder(task.TemplateNameEn);
            row++;
        }

        ReportExcelHelpers.AutoFitColumns(sheet);
    }

    private static void AddLateTeamsSheet(XLWorkbook workbook, IReadOnlyList<DashboardLateTeamDto> lateTeams)
    {
        var sheet = workbook.Worksheets.Add("LateTeams");
        ReportExcelHelpers.AddHeaderRow(
            sheet, 1, "Team Name", "Survey Count", "Overdue Count", "Completed Late Count", "On-Time Count",
            "Avg Days Late", "Max Days Overdue", "On-Time Rate %");

        var row = 2;
        foreach (var team in lateTeams)
        {
            sheet.Cell(row, 1).Value = ReportExcelHelpers.OrPlaceholder(team.FieldTeamName);
            sheet.Cell(row, 2).Value = team.SurveyCount;
            sheet.Cell(row, 3).Value = team.OverdueCount;
            sheet.Cell(row, 4).Value = team.CompletedLateCount;
            sheet.Cell(row, 5).Value = team.OnTimeCount;
            sheet.Cell(row, 6).Value = team.AvgDaysLate;
            sheet.Cell(row, 7).Value = team.MaxDaysOverdue;
            sheet.Cell(row, 8).Value = team.OnTimeRatePercent;
            row++;
        }

        ReportExcelHelpers.AutoFitColumns(sheet);
    }
}
