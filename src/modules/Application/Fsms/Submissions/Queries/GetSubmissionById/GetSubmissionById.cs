using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Submissions.Interfaces;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Submissions.Queries.GetSubmissionById;

[Authorize(Policy = FsmsPolicies.ViewSurveys)]
public record GetSubmissionByIdQuery : IRequest<Result<IReadOnlyDictionary<string, object?>>>
{
    public long TemplateId { get; init; }
    public long SubmissionId { get; init; }
}

public sealed class GetSubmissionByIdQueryHandler(
    IApplicationDbContext context,
    ISurveySubmissionStore submissionStore)
    : IRequestHandler<GetSubmissionByIdQuery, Result<IReadOnlyDictionary<string, object?>>>
{
    public async Task<Result<IReadOnlyDictionary<string, object?>>> Handle(GetSubmissionByIdQuery request, CancellationToken cancellationToken)
    {
        var templateExists = await context.SurveyTemplates
            .AnyAsync(x => x.Id == request.TemplateId, cancellationToken);

        if (!templateExists)
        {
            return Result<IReadOnlyDictionary<string, object?>>.Fail("Template not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        var row = await submissionStore.GetByIdAsync(request.TemplateId, request.SubmissionId, cancellationToken);

        if (row is null)
        {
            return Result<IReadOnlyDictionary<string, object?>>.Fail("Submission not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        return Result<IReadOnlyDictionary<string, object?>>.Success(row);
    }
}
