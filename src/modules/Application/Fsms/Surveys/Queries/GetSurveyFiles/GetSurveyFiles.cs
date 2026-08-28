using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Surveys.Queries.GetSurveyFiles;

public record SurveyFileDto(
    Guid FileId,
    string DataName,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset Created);

[Authorize(Policy = FsmsPolicies.ViewSurveys)]
public record GetSurveyFilesQuery : IRequest<Result<IReadOnlyList<SurveyFileDto>>>
{
    public long SurveyId { get; init; }
}

public sealed class GetSurveyFilesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSurveyFilesQuery, Result<IReadOnlyList<SurveyFileDto>>>
{
    public async Task<Result<IReadOnlyList<SurveyFileDto>>> Handle(GetSurveyFilesQuery request, CancellationToken cancellationToken)
    {
        var files = await context.SubmissionFiles
            .AsNoTracking()
            .Where(f => f.SurveyId == request.SurveyId && f.IsActive)
            .OrderByDescending(f => f.Created)
            .Select(f => new SurveyFileDto(
                f.FileId,
                f.DataName,
                f.FileName,
                f.ContentType,
                f.SizeBytes,
                f.Created))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<SurveyFileDto>>.Success(files);
    }
}
