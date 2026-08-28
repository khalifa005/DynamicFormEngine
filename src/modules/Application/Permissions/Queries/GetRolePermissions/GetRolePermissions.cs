using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Permissions.Models;
using KH.Domain.Constants;
using Shared.Core.Common;

namespace KH.Application.Permissions.Queries.GetRolePermissions;

[Authorize(Policy = Policies.CanManageRolePermissions)]
public record GetRolePermissionsQuery : IRequest<Result<RolePermissionsDto>>
{
    public string RoleName { get; init; } = default!;
}

public sealed class GetRolePermissionsQueryValidator : AbstractValidator<GetRolePermissionsQuery>
{
    public GetRolePermissionsQueryValidator()
    {
        RuleFor(x => x.RoleName)
            .NotEmpty().WithMessage("RoleName is required.");
    }
}

public sealed class GetRolePermissionsQueryHandler(
    IRoleLookup roleLookup,
    IPermissionResolver permissionResolver)
    : IRequestHandler<GetRolePermissionsQuery, Result<RolePermissionsDto>>
{
    public async Task<Result<RolePermissionsDto>> Handle(
        GetRolePermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var roleName = request.RoleName.Trim();
        var roleId = await roleLookup.GetRoleIdByNameAsync(roleName, cancellationToken);

        if (roleId is null)
        {
            return Result<RolePermissionsDto>.Fail(
                "Role not found.",
                ApiErrorCodes.NotFound,
                httpStatusCode: 404);
        }

        var assignedPermissionCodes = await permissionResolver.GetPermissionCodesForRoleAsync(roleId, cancellationToken);

        return Result<RolePermissionsDto>.Success(new RolePermissionsDto
        {
            RoleName = roleName,
            AssignedPermissionCodes = assignedPermissionCodes
        });
    }
}
