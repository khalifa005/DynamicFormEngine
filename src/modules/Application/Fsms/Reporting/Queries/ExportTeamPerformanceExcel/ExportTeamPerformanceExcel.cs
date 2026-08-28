using ClosedXML.Excel;
using KH.Application.Common.Security;
using KH.Application.Fsms.Reporting.Common;
using KH.Application.Fsms.Reporting.Models;
using KH.Application.Fsms.Reporting.Queries.GetTeamPerformanceReport;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Reporting.Queries.ExportTeamPerformanceExcel;

/// <summary>
/// Excel export for the Reports &gt; Team Performance page. Delegates every number to
/// <see cref="GetTeamPerformanceReportQuery"/> — the same query the screen itself reads — and only
/// re-shapes the result into a workbook.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewReports)]
public record ExportTeamPerformanceExcelQuery : IRequest<Result<ReportExportResultDto>>
{
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
    public string? Status { get; init; }
    public long? TeamId { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
}

public sealed class ExportTeamPerformanceExcelQueryValidator : AbstractValidator<ExportTeamPerformanceExcelQuery>
{
    public ExportTeamPerformanceExcelQueryValidator()
    {
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value)
            .WithMessage("The end of the date range must not precede its start.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);

        RuleFor(x => x.Status)
            .Must(SurveyStatuses.IsDefined).WithMessage("Status is not a recognized survey status.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
    }
}

public sealed class ExportTeamPerformanceExcelQueryHandler(ISender sender)
    : IRequestHandler<ExportTeamPerformanceExcelQuery, Result<ReportExportResultDto>>
{
    private const string ReportTitle = "Team Performance Report";

    public async Task<Result<ReportExportResultDto>> Handle(
        ExportTeamPerformanceExcelQuery request, CancellationToken cancellationToken)
    {
        var reportResult = await sender.Send(
            new GetTeamPerformanceReportQuery
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                Status = request.Status,
                TeamId = request.TeamId,
                BranchCode = request.BranchCode,
                OperationAreaCode = request.OperationAreaCode,
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

        AddTeamPerformanceSheet(workbook, report.Teams);
        AddWeeklyCompletionTrendSheet(workbook, report.WeeklyCompletionTrend);
        AddProductivityTrendSheet(workbook, report.ProductivityTrend);

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"TeamPerformance_{generatedAt:yyyyMMdd_HHmmss}.xlsx";

        return Result<ReportExportResultDto>.Success(
            new ReportExportResultDto(stream, ReportExportContentTypes.Excel, fileName));
    }

    private static List<(string Label, string Value)> BuildAppliedFilters(ExportTeamPerformanceExcelQuery request)
    {
        var filters = new List<(string Label, string Value)>();

        ReportExcelHelpers.AddFilter(filters, "From Date", request.FromDate);
        ReportExcelHelpers.AddFilter(filters, "To Date", request.ToDate);
        ReportExcelHelpers.AddFilter(filters, "Status", request.Status);
        ReportExcelHelpers.AddFilter(filters, "Team Id", request.TeamId?.ToString());
        ReportExcelHelpers.AddFilter(filters, "Branch", request.BranchCode);
        ReportExcelHelpers.AddFilter(filters, "Operation Area", request.OperationAreaCode);

        return filters;
    }

    private static List<(string Label, string Value)> BuildKpiRows(TeamPerformanceKpisDto kpis) =>
    [
        ("Assigned Tasks", kpis.AssignedTasks.ToString()),
        ("Completed Tasks", kpis.CompletedTasks.ToString()),
        ("Pending Tasks", kpis.PendingTasks.ToString()),
        ("Overdue Tasks", kpis.OverdueTasks.ToString()),
        ("Completion Rate", $"{kpis.CompletionRatePercent:0.##}%"),
        ("Avg Completion Hours", kpis.AvgCompletionHours.ToString("0.##")),
    ];

    private static void AddTeamPerformanceSheet(XLWorkbook workbook, IReadOnlyList<TeamPerformanceRowDto> teams)
    {
        var sheet = workbook.Worksheets.Add("TeamPerformance");
        ReportExcelHelpers.AddHeaderRow(
            sheet, 1,
            "Team Name", "Branch Code", "Assigned", "Completed", "Pending", "In Progress", "Returned",
            "Completion %", "Avg Completion Hours", "Daily Productivity", "Overdue Count", "On-Time Rate %");

        var row = 2;
        foreach (var team in teams)
        {
            sheet.Cell(row, 1).Value = team.TeamName;
            sheet.Cell(row, 2).Value = ReportExcelHelpers.OrPlaceholder(team.BranchCode);
            sheet.Cell(row, 3).Value = team.Assigned;
            sheet.Cell(row, 4).Value = team.Completed;
            sheet.Cell(row, 5).Value = team.Pending;
            sheet.Cell(row, 6).Value = team.InProgress;
            sheet.Cell(row, 7).Value = team.Returned;
            sheet.Cell(row, 8).Value = team.CompletionPercent;
            sheet.Cell(row, 9).Value = team.AvgCompletionHours;
            sheet.Cell(row, 10).Value = team.DailyProductivity;
            sheet.Cell(row, 11).Value = team.OverdueCount;
            sheet.Cell(row, 12).Value = team.OnTimeRatePercent;
            row++;
        }

        ReportExcelHelpers.AutoFitColumns(sheet);
    }

    private static void AddWeeklyCompletionTrendSheet(
        XLWorkbook workbook, IReadOnlyList<TeamWeeklyCompletionPointDto> weeklyCompletionTrend)
    {
        var sheet = workbook.Worksheets.Add("WeeklyCompletionTrend");
        ReportExcelHelpers.AddHeaderRow(sheet, 1, "Team Name", "Week Start", "Completed Count");

        var row = 2;
        foreach (var point in weeklyCompletionTrend)
        {
            sheet.Cell(row, 1).Value = point.TeamName;
            sheet.Cell(row, 2).Value = ReportExcelHelpers.FormatDate(point.WeekStart);
            sheet.Cell(row, 3).Value = point.CompletedCount;
            row++;
        }

        ReportExcelHelpers.AutoFitColumns(sheet);
    }

    private static void AddProductivityTrendSheet(
        XLWorkbook workbook, IReadOnlyList<ProductivityTrendPointDto> productivityTrend)
    {
        var sheet = workbook.Worksheets.Add("ProductivityTrend");
        ReportExcelHelpers.AddHeaderRow(sheet, 1, "Week Start", "Avg Completed Per Team");

        var row = 2;
        foreach (var point in productivityTrend)
        {
            sheet.Cell(row, 1).Value = ReportExcelHelpers.FormatDate(point.WeekStart);
            sheet.Cell(row, 2).Value = point.AvgCompletedPerTeam;
            row++;
        }

        ReportExcelHelpers.AutoFitColumns(sheet);
    }
}
