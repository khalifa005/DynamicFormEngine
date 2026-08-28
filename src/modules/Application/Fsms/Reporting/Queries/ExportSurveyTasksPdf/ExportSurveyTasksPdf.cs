using KH.Application.Common.Security;
using KH.Application.Fsms.Reporting.Common;
using KH.Application.Fsms.Reporting.Models;
using KH.Application.Fsms.Reporting.Queries.GetSurveyTasksReportExportRows;
using KH.Application.Fsms.Reporting.Queries.GetSurveyTasksReportSummary;
using KH.Application.Fsms.Surveys.Queries.ExportSurveyPdf;
using KH.Domain.Constants.Fsms;
using QuestPDF.Fluent;
using Shared.Core.Common;

namespace KH.Application.Fsms.Reporting.Queries.ExportSurveyTasksPdf;

/// <summary>
/// PDF export of the Reports > Survey Tasks page. Combines the summary widgets
/// (<see cref="GetSurveyTasksReportSummaryQuery"/>) with the full filtered row set
/// (<see cref="GetSurveyTasksReportExportRowsQuery"/>) into one document — the same two calls the UI
/// makes, so the export never drifts from what is on screen.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewReports)]
public record ExportSurveyTasksPdfQuery : IRequest<Result<ReportExportResultDto>>
{
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
    public string? Status { get; init; }
    public long? TeamId { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public string? Source { get; init; }
}

public sealed class ExportSurveyTasksPdfQueryValidator : AbstractValidator<ExportSurveyTasksPdfQuery>
{
    public ExportSurveyTasksPdfQueryValidator()
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

public sealed class ExportSurveyTasksPdfQueryHandler(ISender sender)
    : IRequestHandler<ExportSurveyTasksPdfQuery, Result<ReportExportResultDto>>
{
    public async Task<Result<ReportExportResultDto>> Handle(
        ExportSurveyTasksPdfQuery request, CancellationToken cancellationToken)
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

        if (!summaryResult.IsSuccess)
        {
            return Result<ReportExportResultDto>.Fail(summaryResult.Errors);
        }

        var detailRowsResult = await sender.Send(
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

        if (!detailRowsResult.IsSuccess)
        {
            return Result<ReportExportResultDto>.Fail(detailRowsResult.Errors);
        }

        SurveyPdfFonts.EnsureRegistered();

        var document = new SurveyTasksPdfDocument(summaryResult.Data!, detailRowsResult.Data!, BuildAppliedFilters(request));
        var pdfStream = new MemoryStream();
        document.GeneratePdf(pdfStream);
        pdfStream.Position = 0;

        var fileName = $"SurveyTasks_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.pdf";

        return Result<ReportExportResultDto>.Success(
            new ReportExportResultDto(pdfStream, ReportExportContentTypes.Pdf, fileName));
    }

    private static List<string> BuildAppliedFilters(ExportSurveyTasksPdfQuery request)
    {
        var lines = new List<string>();

        ReportPdfHelpers.AddDateRangeFilterIfPresent(lines, request.FromDate, request.ToDate);
        ReportPdfHelpers.AddFilterIfPresent(lines, "Status", request.Status);
        ReportPdfHelpers.AddFilterIfPresent(lines, "Team Id", request.TeamId);
        ReportPdfHelpers.AddFilterIfPresent(lines, "Branch", request.BranchCode);
        ReportPdfHelpers.AddFilterIfPresent(lines, "Operation Area", request.OperationAreaCode);
        ReportPdfHelpers.AddFilterIfPresent(lines, "Source", request.Source);

        return lines;
    }
}
