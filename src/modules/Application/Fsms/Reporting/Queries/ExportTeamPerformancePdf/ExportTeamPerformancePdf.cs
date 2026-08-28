using KH.Application.Common.Security;
using KH.Application.Fsms.Reporting.Common;
using KH.Application.Fsms.Reporting.Models;
using KH.Application.Fsms.Reporting.Queries.GetTeamPerformanceReport;
using KH.Application.Fsms.Surveys.Queries.ExportSurveyPdf;
using KH.Domain.Constants.Fsms;
using QuestPDF.Fluent;
using Shared.Core.Common;

namespace KH.Application.Fsms.Reporting.Queries.ExportTeamPerformancePdf;

/// <summary>
/// PDF export of the Reports > Team Performance page. Delegates to
/// <see cref="GetTeamPerformanceReportQuery"/> for the data — this slice only renders it, so the
/// export can never drift from what the on-screen report shows for the same filters.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewReports)]
public record ExportTeamPerformancePdfQuery : IRequest<Result<ReportExportResultDto>>
{
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
    public string? Status { get; init; }
    public long? TeamId { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
}

public sealed class ExportTeamPerformancePdfQueryValidator : AbstractValidator<ExportTeamPerformancePdfQuery>
{
    public ExportTeamPerformancePdfQueryValidator()
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

public sealed class ExportTeamPerformancePdfQueryHandler(ISender sender)
    : IRequestHandler<ExportTeamPerformancePdfQuery, Result<ReportExportResultDto>>
{
    public async Task<Result<ReportExportResultDto>> Handle(
        ExportTeamPerformancePdfQuery request, CancellationToken cancellationToken)
    {
        var inner = await sender.Send(
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

        if (!inner.IsSuccess)
        {
            return Result<ReportExportResultDto>.Fail(inner.Errors);
        }

        SurveyPdfFonts.EnsureRegistered();

        var document = new TeamPerformancePdfDocument(inner.Data!, BuildAppliedFilters(request));
        var pdfStream = new MemoryStream();
        document.GeneratePdf(pdfStream);
        pdfStream.Position = 0;

        var fileName = $"TeamPerformance_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.pdf";

        return Result<ReportExportResultDto>.Success(
            new ReportExportResultDto(pdfStream, ReportExportContentTypes.Pdf, fileName));
    }

    private static List<string> BuildAppliedFilters(ExportTeamPerformancePdfQuery request)
    {
        var lines = new List<string>();

        ReportPdfHelpers.AddDateRangeFilterIfPresent(lines, request.FromDate, request.ToDate);
        ReportPdfHelpers.AddFilterIfPresent(lines, "Status", request.Status);
        ReportPdfHelpers.AddFilterIfPresent(lines, "Team Id", request.TeamId);
        ReportPdfHelpers.AddFilterIfPresent(lines, "Branch", request.BranchCode);
        ReportPdfHelpers.AddFilterIfPresent(lines, "Operation Area", request.OperationAreaCode);

        return lines;
    }
}
