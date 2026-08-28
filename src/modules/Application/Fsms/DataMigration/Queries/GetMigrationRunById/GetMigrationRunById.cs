using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.DataMigration.Models;
using KH.Application.Fsms.DataMigration.Queries.GetMigrationRuns;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.DataMigration.Queries.GetMigrationRunById;

/// <summary>
/// One run and a page of its per-record outcomes. Doubles as the progress endpoint the page polls
/// while a run is in flight, which is why the record page is optional in effect — a poll asks for a
/// small page and reads the counters off the run.
/// </summary>
[Authorize(Policy = FsmsPolicies.ImportData)]
public record GetMigrationRunByIdQuery : IRequest<Result<MigrationRunDetailDto>>
{
    public long RunId { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    /// <summary>Narrows the records to one outcome — in practice, to the failures.</summary>
    public string? RecordStatus { get; init; }

    /// <summary>
    /// Narrows to records that referenced media the archive did not hold. Separate from
    /// <see cref="RecordStatus"/> because a record can import perfectly well and still be short a
    /// photo — "two files missing out of six thousand" is otherwise unanswerable without paging
    /// through every row.
    /// </summary>
    public bool? WithMissingMedia { get; init; }
}

public sealed record MigrationRunDetailDto
{
    public MigrationRunListItemDto Run { get; init; } = new();
    public PagedResult<MigrationRunRecordDto> Records { get; init; } = new([], 0, 1, 20);
}

public sealed class GetMigrationRunByIdQueryValidator : AbstractValidator<GetMigrationRunByIdQuery>
{
    public GetMigrationRunByIdQueryValidator()
    {
        RuleFor(x => x.RunId)
            .GreaterThan(0).WithMessage("Run id is required.");

        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 200).WithMessage("Page size must be between 1 and 200.");

        RuleFor(x => x.RecordStatus)
            .Must(MigrationRecordStatuses.IsDefined).WithMessage("The record status is not recognized.")
            .When(x => !string.IsNullOrWhiteSpace(x.RecordStatus));
    }
}

public sealed class GetMigrationRunByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetMigrationRunByIdQuery, Result<MigrationRunDetailDto>>
{
    public async Task<Result<MigrationRunDetailDto>> Handle(
        GetMigrationRunByIdQuery request,
        CancellationToken cancellationToken)
    {
        var run = await context.DataMigrationRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.RunId, cancellationToken);

        if (run is null)
        {
            return Result<MigrationRunDetailDto>.Fail("Migration run not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        var template = await context.SurveyTemplates
            .AsNoTracking()
            .Where(x => x.Id == run.TemplateId)
            .Select(x => new { x.TemplateCode, x.TemplateNameEn })
            .FirstOrDefaultAsync(cancellationToken);

        // Read through the set rather than off the aggregate: a run holds a row per source record,
        // and loading several hundred of them to show twenty would defeat the paging.
        var recordQuery = context.DataMigrationRecords
            .AsNoTracking()
            .Where(record => record.RunId == run.Id);

        if (!string.IsNullOrWhiteSpace(request.RecordStatus))
        {
            var status = request.RecordStatus.Trim();
            recordQuery = recordQuery.Where(record => record.Status == status);
        }

        if (request.WithMissingMedia == true)
        {
            recordQuery = recordQuery.Where(record => record.FilesMissing > 0);
        }

        var total = await recordQuery.CountAsync(cancellationToken);

        var records = await recordQuery
            .OrderBy(record => record.RowNumber)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(record => new MigrationRunRecordDto
            {
                Id = record.Id,
                RowNumber = record.RowNumber,
                ExternalId = record.ExternalId,
                Status = record.Status,
                SurveyId = record.SurveyId,
                SurveyCode = record.SurveyCode,
                SubmissionId = record.SubmissionId,
                FilesImported = record.FilesImported,
                FilesMissing = record.FilesMissing,
                Message = record.Message,
            })
            .ToListAsync(cancellationToken);

        return Result<MigrationRunDetailDto>.Success(new MigrationRunDetailDto
        {
            Run = GetMigrationRunsQueryHandler.ToDto(run, template?.TemplateCode, template?.TemplateNameEn),
            Records = new PagedResult<MigrationRunRecordDto>(records, total, request.Page, request.PageSize),
        });
    }
}
