using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Templates.Common;
using KH.Application.Fsms.Templates.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Templates.Queries.GetTemplateById;

[Authorize(Policy = FsmsPolicies.ViewTemplates)]
public record GetTemplateByIdQuery : IRequest<Result<TemplateDetailDto>>
{
    public long TemplateId { get; init; }
}

public sealed class GetTemplateByIdQueryHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService)
    : IRequestHandler<GetTemplateByIdQuery, Result<TemplateDetailDto>>
{
    public async Task<Result<TemplateDetailDto>> Handle(GetTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var template = await context.SurveyTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result<TemplateDetailDto>.Fail("Template not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        // After the not-found check, so a template that does not exist still reads as missing rather
        // than as one the caller is being kept away from.
        if (!await TemplateScopeGuard.CanSeeAsync(orgScopeService, template.Id, cancellationToken))
        {
            return Result<TemplateDetailDto>.Fail(
                TemplateScopeGuard.OutsideTerritoryMessage,
                ApiErrorCodes.UnauthorizedAccess,
                httpStatusCode: 403);
        }

        var scopes = await context.OrgScopes
            .AsNoTracking()
            .Where(x => x.OwnerType == OrgScopeOwnerTypes.Template && x.OwnerId == template.Id.ToString() && x.IsActive)
            .Select(x => new OrgScopeAssignment { Level = x.Level, Code = x.Code, DepartmentId = x.DepartmentId })
            .ToListAsync(cancellationToken);

        return Result<TemplateDetailDto>.Success(template.ToDetailDto(scopes));
    }
}
