using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants;
using KH.Application.Permissions.Models;
using Shared.Core.Common;

namespace KH.Application.Permissions.Queries.GetPermissions;

[Authorize(Policy = Policies.CanManageRolePermissions)]
public record GetPermissionsQuery : IRequest<Result<IReadOnlyList<PermissionDto>>>;

public sealed class GetPermissionsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPermissionsQuery, Result<IReadOnlyList<PermissionDto>>>
{
    public async Task<Result<IReadOnlyList<PermissionDto>>> Handle(
        GetPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var permissions = await context.Permissions
            .AsNoTracking()
            .OrderBy(x => x.Module)
            .ThenBy(x => x.Code)
            .Select(x => new PermissionDto
            {
                Id = x.Id,
                Code = x.Code,
                Module = x.Module,
                NameEn = x.NameEn,
                NameAr = x.NameAr,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<PermissionDto>>.Success(permissions);
    }
}
