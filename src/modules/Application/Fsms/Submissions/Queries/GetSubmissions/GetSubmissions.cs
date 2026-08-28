using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Submissions.Interfaces;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Submissions.Queries.GetSubmissions;

[Authorize(Policy = FsmsPolicies.ViewSurveys)]
public record GetSubmissionsQuery : IRequest<Result<PagedResult<IReadOnlyDictionary<string, object?>>>>
{
    public long TemplateId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class GetSubmissionsQueryValidator : AbstractValidator<GetSubmissionsQuery>
{
    public GetSubmissionsQueryValidator()
    {
        RuleFor(x => x.TemplateId).GreaterThan(0).WithMessage("Template id is required.");
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 1000).WithMessage("Page size must be between 1 and 1000.");
    }
}

public sealed class GetSubmissionsQueryHandler(
    IApplicationDbContext context,
    ISurveySubmissionStore submissionStore)
    : IRequestHandler<GetSubmissionsQuery, Result<PagedResult<IReadOnlyDictionary<string, object?>>>>
{
    public async Task<Result<PagedResult<IReadOnlyDictionary<string, object?>>>> Handle(GetSubmissionsQuery request, CancellationToken cancellationToken)
    {
        var templateExists = await context.SurveyTemplates
            .AnyAsync(x => x.Id == request.TemplateId, cancellationToken);

        if (!templateExists)
        {
            return Result<PagedResult<IReadOnlyDictionary<string, object?>>>.Fail(
                "Template not found.",
                ApiErrorCodes.NotFound,
                httpStatusCode: 404);
        }

        var (items, total) = await submissionStore.ListAsync(request.TemplateId, request.Page, request.PageSize, cancellationToken);

        var paged = new PagedResult<IReadOnlyDictionary<string, object?>>(items, total, request.Page, request.PageSize);

        return Result<PagedResult<IReadOnlyDictionary<string, object?>>>.Success(paged);
    }
}
