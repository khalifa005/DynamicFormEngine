using KH.Application.Common.Security;
using KH.Application.Fsms.Reporting.Common;
using KH.Application.Fsms.Reporting.Models;
using KH.Application.Fsms.Reporting.Queries.GetGeneralStatisticsReport;
using KH.Application.Fsms.Surveys.Queries.ExportSurveyPdf;
using KH.Domain.Constants.Fsms;
using QuestPDF.Fluent;
using Shared.Core.Common;

namespace KH.Application.Fsms.Reporting.Queries.ExportGeneralStatisticsPdf;

/// <summary>
/// PDF export of the Reports > General Statistics page. Delegates to
/// <see cref="GetGeneralStatisticsReportQuery"/> for the data — this slice only renders it — so the
/// export can never drift from what the on-screen report shows for the same filters.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewReports)]
public record ExportGeneralStatisticsPdfQuery : IRequest<Result<ReportExportResultDto>>
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

public sealed class ExportGeneralStatisticsPdfQueryValidator : AbstractValidator<ExportGeneralStatisticsPdfQuery>
{
    public ExportGeneralStatisticsPdfQueryValidator()
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

public sealed class ExportGeneralStatisticsPdfQueryHandler(ISender sender)
    : IRequestHandler<ExportGeneralStatisticsPdfQuery, Result<ReportExportResultDto>>
{
    public async Task<Result<ReportExportResultDto>> Handle(
        ExportGeneralStatisticsPdfQuery request, CancellationToken cancellationToken)
    {
        var inner = await sender.Send(
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

        if (!inner.IsSuccess)
        {
            return Result<ReportExportResultDto>.Fail(inner.Errors);
        }

        SurveyPdfFonts.EnsureRegistered();

        var document = new GeneralStatisticsPdfDocument(inner.Data!, BuildAppliedFilters(request));
        var pdfStream = new MemoryStream();
        document.GeneratePdf(pdfStream);
        pdfStream.Position = 0;

        var fileName = $"GeneralStatistics_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.pdf";

        return Result<ReportExportResultDto>.Success(
            new ReportExportResultDto(pdfStream, ReportExportContentTypes.Pdf, fileName));
    }

    private static List<string> BuildAppliedFilters(ExportGeneralStatisticsPdfQuery request)
    {
        var lines = new List<string>();

        ReportPdfHelpers.AddDateRangeFilterIfPresent(lines, request.FromDate, request.ToDate);
        ReportPdfHelpers.AddFilterIfPresent(lines, "Cluster", request.ClusterCode);
        ReportPdfHelpers.AddFilterIfPresent(lines, "CBU", request.CbuCode);
        ReportPdfHelpers.AddFilterIfPresent(lines, "Branch", request.BranchCode);
        ReportPdfHelpers.AddFilterIfPresent(lines, "Operation Area", request.OperationAreaCode);
        ReportPdfHelpers.AddFilterIfPresent(lines, "Status", request.Status);
        ReportPdfHelpers.AddFilterIfPresent(lines, "Source", request.Source);

        return lines;
    }
}
