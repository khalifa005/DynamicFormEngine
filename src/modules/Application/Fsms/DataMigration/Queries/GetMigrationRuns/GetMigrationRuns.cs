using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.DataMigration.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.DataMigration.Queries.GetMigrationRuns;

/// <summary>
/// The import history, newest first. Server-side paged: a deployment accumulates a run per import
/// attempt and the grid is what an operator scrolls to find the one they started.
/// </summary>
[Authorize(Policy = FsmsPolicies.ImportData)]
public record GetMigrationRunsQuery : IRequest<Result<PagedResult<MigrationRunListItemDto>>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SourceCode { get; init; }
    public string? Status { get; init; }
}

public sealed class GetMigrationRunsQueryValidator : AbstractValidator<GetMigrationRunsQuery>
{
    public GetMigrationRunsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 200).WithMessage("Page size must be between 1 and 200.");

        RuleFor(x => x.SourceCode)
            .Must(MigrationSourceCodes.IsDefined).WithMessage("The migration source is not recognized.")
            .When(x => !string.IsNullOrWhiteSpace(x.SourceCode));

        RuleFor(x => x.Status)
            .Must(MigrationRunStatuses.IsDefined).WithMessage("The run status is not recognized.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
    }
}

public sealed class GetMigrationRunsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetMigrationRunsQuery, Result<PagedResult<MigrationRunListItemDto>>>
{
    public async Task<Result<PagedResult<MigrationRunListItemDto>>> Handle(
        GetMigrationRunsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.DataMigrationRuns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SourceCode))
        {
            var sourceCode = request.SourceCode.Trim();
            query = query.Where(run => run.SourceCode == sourceCode);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(run => run.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);

        // The template's identity is joined in rather than stored on the run: it belongs to the
        // template, and duplicating it here would leave the grid showing a name a rename moved on
        // from. It matters more now that the operator picks the template — the run list is where
        // they check they picked the one they meant.
        var rows = await query
            .OrderByDescending(run => run.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(run => new
            {
                Run = run,
                Template = context.SurveyTemplates
                    .Where(template => template.Id == run.TemplateId)
                    .Select(template => new { template.TemplateCode, template.TemplateNameEn })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => ToDto(row.Run, row.Template?.TemplateCode, row.Template?.TemplateNameEn))
            .ToList();

        return Result<PagedResult<MigrationRunListItemDto>>.Success(
            new PagedResult<MigrationRunListItemDto>(items, total, request.Page, request.PageSize));
    }

    internal static MigrationRunListItemDto ToDto(
        Domain.Entities.Fsms.Migration.DataMigrationRun run,
        string? templateCode,
        string? templateName) => new()
    {
        Id = run.Id,
        SourceCode = run.SourceCode,
        Mode = run.Mode,
        Status = run.Status,
        FileName = run.FileName,
        TemplateId = run.TemplateId,
        TemplateCode = templateCode,
        TemplateName = templateName,
        UnmappedColumns = run.UnmappedColumns,
        MappedColumnCount = run.MappedColumnCount,
        SourceColumnCount = run.SourceColumnCount,
        UnmappedColumnCount = run.UnmappedColumnCount,
        IgnoredColumns = run.IgnoredColumns,
        StoredIgnoredColumnCount = run.IgnoredColumnCount,
        TotalRecords = run.TotalRecords,
        ImportedCount = run.ImportedCount,
        SkippedCount = run.SkippedCount,
        FailedCount = run.FailedCount,
        ValidatedCount = run.ValidatedCount,
        FilesImported = run.FilesImported,
        FilesMissing = run.FilesMissing,
        RequestedBy = run.RequestedBy,
        Created = run.Created,
        StartedAt = run.StartedAt,
        CompletedAt = run.CompletedAt,
        ErrorMessage = run.ErrorMessage,
        IsTerminal = MigrationRunStatuses.IsTerminal(run.Status),
    };
}
