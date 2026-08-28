using ClosedXML.Excel;
using KH.Application.Common.Security;
using KH.Application.Fsms.Reporting.Common;
using KH.Application.Fsms.Reporting.Models;
using KH.Application.Fsms.Reporting.Queries.GetSurveyTasksReportExportRows;
using KH.Application.Fsms.Reporting.Queries.GetSurveyTasksReportSummary;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Reporting.Queries.ExportSurveyTasksExcel;

/// <summary>
/// Excel export for the Reports &gt; Survey Tasks page. Calls both the summary query (KPIs and
/// charts — the same one the screen's top half reads) and the unpaged export-rows query (the full
/// filtered row set — never the UI-paginated details query, which is capped at 100 rows) so the
/// workbook's TaskDetails sheet reflects every row the caller's filters match.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewReports)]
public record ExportSurveyTasksExcelQuery : IRequest<Result<ReportExportResultDto>>
{
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
    public string? Status { get; init; }
    public long? TeamId { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public string? Source { get; init; }
}

public sealed class ExportSurveyTasksExcelQueryValidator : AbstractValidator<ExportSurveyTasksExcelQuery>
{
    public ExportSurveyTasksExcelQueryValidator()
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

        RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("Team id must be greater than 0.")
            .When(x => x.TeamId.HasValue);
    }
}

public sealed class ExportSurveyTasksExcelQueryHandler(ISender sender)
    : IRequestHandler<ExportSurveyTasksExcelQuery, Result<ReportExportResultDto>>
{
    private const string ReportTitle = "Survey Tasks Report";

    /// <summary>
    /// TaskDetails can hold up to 20,000 rows; fixed widths avoid ClosedXML's O(rows × cols)
    /// AdjustToContents cost. Order matches the sheet's header row.
    /// </summary>
    private static readonly double[] TaskDetailsColumnWidths =
    [
        18, // Survey Code
        28, // Template
        10, // Template Version
        14, // Status
        12, // Source
        14, // Branch Code
        28, // Branch Name
        12, // Department Id
        24, // Team Name
        24, // Customer Name
        16, // FA Id
        20, // Created
        20, // Due Date
        14, // Submission Count
    ];

    public async Task<Result<ReportExportResultDto>> Handle(
        ExportSurveyTasksExcelQuery request, CancellationToken cancellationToken)
    {
        var summaryResult = await sender.Send(
            new GetSurveyTasksReportSummaryQuery
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                Status = request.Status,
                TeamId = request.TeamId,
                BranchCode = request.BranchCode,
                OperationAreaCode = request.OperationAreaCode,
                Source = request.Source,
            },
            cancellationToken);

        if (!summaryResult.IsSuccess || summaryResult.Data is null)
        {
            return Result<ReportExportResultDto>.Fail(summaryResult.Errors);
        }

        var rowsResult = await sender.Send(
            new GetSurveyTasksReportExportRowsQuery
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                Status = request.Status,
                TeamId = request.TeamId,
                BranchCode = request.BranchCode,
                OperationAreaCode = request.OperationAreaCode,
                Source = request.Source,
            },
            cancellationToken);

        if (!rowsResult.IsSuccess || rowsResult.Data is null)
        {
            return Result<ReportExportResultDto>.Fail(rowsResult.Errors);
        }

        var summary = summaryResult.Data;
        var generatedAt = DateTimeOffset.UtcNow;

        using var workbook = new XLWorkbook();

        ReportExcelHelpers.AddSummarySheet(
            workbook, ReportTitle, generatedAt, BuildAppliedFilters(request), BuildKpiRows(summary.Kpis));

        AddStatusDistributionSheet(workbook, summary.StatusDistribution);
        AddDailyTrendSheet(workbook, summary.DailyTrend);
        AddTasksByTeamSheet(workbook, summary.TasksByTeam);
        AddTasksByBranchSheet(workbook, summary.TasksByBranch);
        AddLateTasksSheet(workbook, summary.LateTasks);
        AddTaskDetailsSheet(workbook, rowsResult.Data);

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"SurveyTasks_{generatedAt:yyyyMMdd_HHmmss}.xlsx";

        return Result<ReportExportResultDto>.Success(
            new ReportExportResultDto(stream, ReportExportContentTypes.Excel, fileName));
    }

    private static List<(string Label, string Value)> BuildAppliedFilters(ExportSurveyTasksExcelQuery request)
    {
        var filters = new List<(string Label, string Value)>();

        ReportExcelHelpers.AddFilter(filters, "From Date", request.FromDate);
        ReportExcelHelpers.AddFilter(filters, "To Date", request.ToDate);
        ReportExcelHelpers.AddFilter(filters, "Status", request.Status);
        ReportExcelHelpers.AddFilter(filters, "Team Id", request.TeamId?.ToString());
        ReportExcelHelpers.AddFilter(filters, "Branch", request.BranchCode);
        ReportExcelHelpers.AddFilter(filters, "Operation Area", request.OperationAreaCode);
        ReportExcelHelpers.AddFilter(filters, "Source", request.Source);

        return filters;
    }

    private static List<(string Label, string Value)> BuildKpiRows(SurveyTasksReportKpisDto kpis) =>
    [
        ("Total Tasks", kpis.TotalTasks.ToString()),
        ("Pending Tasks", kpis.PendingTasks.ToString()),
        ("In Progress Tasks", kpis.InProgressTasks.ToString()),
        ("Submitted Tasks", kpis.SubmittedTasks.ToString()),
        ("Approved Tasks", kpis.ApprovedTasks.ToString()),
        ("Returned Tasks", kpis.ReturnedTasks.ToString()),
        ("Overdue Tasks", kpis.OverdueTasks.ToString()),
        ("Completion Rate", $"{kpis.CompletionRatePercent:0.##}%"),
    ];

    private static void AddStatusDistributionSheet(
        XLWorkbook workbook, IReadOnlyList<SurveyTaskStatusSliceDto> statusDistribution)
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

    private static void AddDailyTrendSheet(
        XLWorkbook workbook, IReadOnlyList<SurveyTasksDailyTrendPointDto> dailyTrend)
    {
        var sheet = workbook.Worksheets.Add("DailyTrend");
        ReportExcelHelpers.AddHeaderRow(sheet, 1, "Date", "Created Count");

        var row = 2;
        foreach (var point in dailyTrend)
        {
            sheet.Cell(row, 1).Value = ReportExcelHelpers.FormatDate(point.Date);
            sheet.Cell(row, 2).Value = point.CreatedCount;
            row++;
        }

        ReportExcelHelpers.AutoFitColumns(sheet);
    }

    private static void AddTasksByTeamSheet(XLWorkbook workbook, IReadOnlyList<SurveyTasksByTeamDto> tasksByTeam)
    {
        var sheet = workbook.Worksheets.Add("TasksByTeam");
        ReportExcelHelpers.AddHeaderRow(sheet, 1, "Team Name", "Count");

        var row = 2;
        foreach (var item in tasksByTeam)
        {
            sheet.Cell(row, 1).Value = item.TeamName;
            sheet.Cell(row, 2).Value = item.Count;
            row++;
        }

        ReportExcelHelpers.AutoFitColumns(sheet);
    }

    private static void AddTasksByBranchSheet(
        XLWorkbook workbook, IReadOnlyList<SurveyTasksByBranchDto> tasksByBranch)
    {
        var sheet = workbook.Worksheets.Add("TasksByBranch");
        ReportExcelHelpers.AddHeaderRow(sheet, 1, "Branch Name", "Count");

        var row = 2;
        foreach (var item in tasksByBranch)
        {
            sheet.Cell(row, 1).Value = item.BranchName;
            sheet.Cell(row, 2).Value = item.Count;
            row++;
        }

        ReportExcelHelpers.AutoFitColumns(sheet);
    }

    private static void AddLateTasksSheet(XLWorkbook workbook, IReadOnlyList<SurveyTaskLateItemDto> lateTasks)
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
            sheet.Cell(row, 6).Value = ReportExcelHelpers.OrPlaceholder(task.TeamName);
            sheet.Cell(row, 7).Value = ReportExcelHelpers.OrPlaceholder(task.BranchCode);
            sheet.Cell(row, 8).Value = ReportExcelHelpers.OrPlaceholder(task.TemplateNameEn);
            row++;
        }

        ReportExcelHelpers.AutoFitColumns(sheet);
    }

    /// <summary>
    /// Up to 20,000 rows — no <see cref="ReportExcelHelpers.AutoFitColumns"/> here, see
    /// <see cref="TaskDetailsColumnWidths"/>.
    /// </summary>
    private static void AddTaskDetailsSheet(XLWorkbook workbook, IReadOnlyList<SurveyTaskReportRowDto> rows)
    {
        var sheet = workbook.Worksheets.Add("TaskDetails");
        ReportExcelHelpers.AddHeaderRow(
            sheet, 1,
            "Survey Code", "Template", "Template Version", "Status", "Source", "Branch Code", "Branch Name",
            "Department Id", "Team Name", "Customer Name", "FA Id", "Created", "Due Date", "Submission Count");

        var row = 2;
        foreach (var item in rows)
        {
            sheet.Cell(row, 1).Value = item.SurveyCode;
            sheet.Cell(row, 2).Value = ReportExcelHelpers.OrPlaceholder(item.TemplateNameEn);
            sheet.Cell(row, 3).Value = item.TemplateVersionNo;
            sheet.Cell(row, 4).Value = item.Status;
            sheet.Cell(row, 5).Value = item.Source;
            sheet.Cell(row, 6).Value = ReportExcelHelpers.OrPlaceholder(item.BranchCode);
            sheet.Cell(row, 7).Value = ReportExcelHelpers.OrPlaceholder(item.BranchNameEn);
            sheet.Cell(row, 8).Value = item.DepartmentId;
            sheet.Cell(row, 9).Value = ReportExcelHelpers.OrPlaceholder(item.TeamName);
            sheet.Cell(row, 10).Value = ReportExcelHelpers.OrPlaceholder(item.CustomerName);
            sheet.Cell(row, 11).Value = ReportExcelHelpers.OrPlaceholder(item.FaId);
            sheet.Cell(row, 12).Value = ReportExcelHelpers.FormatDate(item.Created);
            sheet.Cell(row, 13).Value = ReportExcelHelpers.FormatDate(item.DueDate);
            sheet.Cell(row, 14).Value = item.SubmissionCount;
            row++;
        }

        ReportExcelHelpers.SetColumnWidths(sheet, TaskDetailsColumnWidths);
    }
}
