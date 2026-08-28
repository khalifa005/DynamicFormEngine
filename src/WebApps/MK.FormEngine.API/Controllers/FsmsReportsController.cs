using KH.Application.Fsms.Reporting.Models;
using KH.Application.Fsms.Reporting.Queries.ExportGeneralStatisticsExcel;
using KH.Application.Fsms.Reporting.Queries.ExportGeneralStatisticsPdf;
using KH.Application.Fsms.Reporting.Queries.ExportSurveyTasksExcel;
using KH.Application.Fsms.Reporting.Queries.ExportSurveyTasksPdf;
using KH.Application.Fsms.Reporting.Queries.ExportTeamPerformanceExcel;
using KH.Application.Fsms.Reporting.Queries.ExportTeamPerformancePdf;
using KH.Application.Fsms.Reporting.Queries.GetDashboard;
using KH.Application.Fsms.Reporting.Queries.GetGeneralStatisticsReport;
using KH.Application.Fsms.Reporting.Queries.GetSurveyTasksReportDetails;
using KH.Application.Fsms.Reporting.Queries.GetSurveyTasksReportSummary;
using KH.Application.Fsms.Reporting.Queries.GetTeamPerformanceReport;
using KH.Domain.Constants.Fsms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.Common;

namespace MK.FormEngine.API.Controllers;

[Authorize]
[Route("api/v1/fsms/reports")]
public sealed class FsmsReportsController : ApiControllerBase
{
    /// <summary>
    /// Every dashboard widget in one call: KPIs against the preceding period, the submission trend,
    /// the status split, team coverage per CBU / branch / operation area, and the caller's own
    /// throughput ranked against their peers. Scoped to the surveys the caller may see.
    /// </summary>
    [HttpGet("dashboard")]
    [Authorize(Policy = FsmsPolicies.ViewDashboard)]
    [ProducesResponseType(typeof(Result<DashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard([FromQuery] GetDashboardQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Reports &gt; General Statistics: KPIs against the preceding period, the weekly trend, the full
    /// status split, per-team completion, recent activity, and the two late-work tables — reusing the
    /// same filtered survey set the dashboard reads.
    /// </summary>
    [HttpGet("general-statistics")]
    [Authorize(Policy = FsmsPolicies.ViewReports)]
    [ProducesResponseType(typeof(Result<GeneralStatisticsReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGeneralStatistics(
        [FromQuery] GetGeneralStatisticsReportQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Reports &gt; Survey Tasks summary: KPIs, the daily creation trend, the status doughnut, task
    /// counts by team and by branch, and the late-work worklist, in one call. The paginated row-by-row
    /// table is a separate endpoint (<see cref="GetSurveyTasksDetails"/>) so a KPI refresh never pays
    /// for fetching every row.
    /// </summary>
    [HttpGet("survey-tasks")]
    [Authorize(Policy = FsmsPolicies.ViewReports)]
    [ProducesResponseType(typeof(Result<SurveyTasksReportSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSurveyTasksSummary(
        [FromQuery] GetSurveyTasksReportSummaryQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Reports &gt; Survey Tasks detail table: true server-side pagination over the same filters the
    /// summary applies.
    /// </summary>
    [HttpGet("survey-tasks/details")]
    [Authorize(Policy = FsmsPolicies.ViewReports)]
    [ProducesResponseType(typeof(Result<PagedResult<SurveyTaskReportRowDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSurveyTasksDetails(
        [FromQuery] GetSurveyTasksReportDetailsQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Reports &gt; Team Performance: KPIs, the per-team breakdown, and the two trend charts, in one call.
    /// </summary>
    [HttpGet("team-performance")]
    [Authorize(Policy = FsmsPolicies.ViewReports)]
    [ProducesResponseType(typeof(Result<TeamPerformanceReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeamPerformance(
        [FromQuery] GetTeamPerformanceReportQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Reports &gt; General Statistics, exported as one Excel workbook over the same filters.</summary>
    [HttpGet("general-statistics/export/excel")]
    [Authorize(Policy = FsmsPolicies.ViewReports)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportGeneralStatisticsExcel(
        [FromQuery] ExportGeneralStatisticsExcelQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            return ToActionResult(result);
        }

        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    /// <summary>Reports &gt; General Statistics, exported as one PDF over the same filters.</summary>
    [HttpGet("general-statistics/export/pdf")]
    [Authorize(Policy = FsmsPolicies.ViewReports)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportGeneralStatisticsPdf(
        [FromQuery] ExportGeneralStatisticsPdfQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            return ToActionResult(result);
        }

        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    /// <summary>Reports &gt; Survey Tasks, exported as one Excel workbook over the full filtered row set (not just the current page).</summary>
    [HttpGet("survey-tasks/export/excel")]
    [Authorize(Policy = FsmsPolicies.ViewReports)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportSurveyTasksExcel(
        [FromQuery] ExportSurveyTasksExcelQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            return ToActionResult(result);
        }

        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    /// <summary>Reports &gt; Survey Tasks, exported as one PDF over the full filtered row set (not just the current page).</summary>
    [HttpGet("survey-tasks/export/pdf")]
    [Authorize(Policy = FsmsPolicies.ViewReports)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportSurveyTasksPdf(
        [FromQuery] ExportSurveyTasksPdfQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            return ToActionResult(result);
        }

        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    /// <summary>Reports &gt; Team Performance, exported as one Excel workbook over the same filters.</summary>
    [HttpGet("team-performance/export/excel")]
    [Authorize(Policy = FsmsPolicies.ViewReports)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportTeamPerformanceExcel(
        [FromQuery] ExportTeamPerformanceExcelQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            return ToActionResult(result);
        }

        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    /// <summary>Reports &gt; Team Performance, exported as one PDF over the same filters.</summary>
    [HttpGet("team-performance/export/pdf")]
    [Authorize(Policy = FsmsPolicies.ViewReports)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportTeamPerformancePdf(
        [FromQuery] ExportTeamPerformancePdfQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            return ToActionResult(result);
        }

        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }
}
