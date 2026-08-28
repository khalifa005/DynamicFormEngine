using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Templates.Common;
using KH.Application.Fsms.Templates.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Templates.Queries.GetTemplateVersions;

[Authorize(Policy = FsmsPolicies.ViewTemplates)]
public record GetTemplateVersionsQuery : IRequest<Result<IReadOnlyList<TemplateVersionDto>>>
{
    public long TemplateId { get; init; }
}

public sealed class GetTemplateVersionsQueryHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService)
    : IRequestHandler<GetTemplateVersionsQuery, Result<IReadOnlyList<TemplateVersionDto>>>
{
    public async Task<Result<IReadOnlyList<TemplateVersionDto>>> Handle(
        GetTemplateVersionsQuery request,
        CancellationToken cancellationToken)
    {
        var templateExists = await context.SurveyTemplates
            .AnyAsync(x => x.Id == request.TemplateId, cancellationToken);

        if (!templateExists)
        {
            return Result<IReadOnlyList<TemplateVersionDto>>.Fail(
                "Template not found.",
                ApiErrorCodes.NotFound,
                httpStatusCode: 404);
        }

        // After the existence check, so an unknown id still reads as missing rather than as one the
        // caller is being kept away from.
        if (!await TemplateScopeGuard.CanSeeAsync(orgScopeService, request.TemplateId, cancellationToken))
        {
            return Result<IReadOnlyList<TemplateVersionDto>>.Fail(
                TemplateScopeGuard.OutsideTerritoryMessage,
                ApiErrorCodes.UnauthorizedAccess,
                httpStatusCode: 403);
        }

        var versions = await context.TemplateVersions
            .AsNoTracking()
            .Where(x => x.TemplateId == request.TemplateId)
            .OrderByDescending(x => x.VersionNo)
            .ThenBy(x => x.TargetClient)
            .Select(x => new TemplateVersionDto
            {
                VersionId = x.Id,
                VersionNo = x.VersionNo,
                TargetClient = x.TargetClient,
                SchemaJson = x.SchemaJson,
                TemplateSnapshotJson = x.TemplateSnapshotJson,
                PublishedBy = x.PublishedBy,
                PublishedDate = x.PublishedDate
            })
            .ToListAsync(cancellationToken);

        IReadOnlyList<TemplateVersionDto> result = versions;
        return Result<IReadOnlyList<TemplateVersionDto>>.Success(result);
    }
}
