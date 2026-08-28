using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Permissions.Models;
using KH.Domain.Constants;
using KH.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Core.Common;

namespace KH.Application.Permissions.Commands.AssignRolePermissions;

[Authorize(Policy = Policies.CanManageRolePermissions)]
public record AssignRolePermissionsCommand : IRequest<Result<RolePermissionsDto>>
{
    public string RoleName { get; init; } = default!;

    public IReadOnlyList<string> PermissionCodes { get; init; } = [];
}

public sealed class AssignRolePermissionsCommandValidator : AbstractValidator<AssignRolePermissionsCommand>
{
    public AssignRolePermissionsCommandValidator()
    {
        RuleFor(x => x.RoleName)
            .NotEmpty().WithMessage("RoleName is required.");

        RuleForEach(x => x.PermissionCodes)
            .NotEmpty().WithMessage("Permission code cannot be empty.");
    }
}

public sealed class AssignRolePermissionsCommandHandler(
    IApplicationDbContext context,
    IRoleLookup roleLookup,
    IPermissionResolver permissionResolver)
    : IRequestHandler<AssignRolePermissionsCommand, Result<RolePermissionsDto>>
{
    public async Task<Result<RolePermissionsDto>> Handle(
        AssignRolePermissionsCommand request,
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

        var normalizedCodes = request.PermissionCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var permissions = await context.Permissions
            .Where(x => x.IsActive && normalizedCodes.Contains(x.Code))
            .ToListAsync(cancellationToken);

        if (permissions.Count != normalizedCodes.Count)
        {
            return Result<RolePermissionsDto>.Fail(
                "One or more permission codes are invalid or inactive.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        var existingAssignments = await context.RolePermissions
            .Where(x => x.RoleId == roleId)
            .ToListAsync(cancellationToken);

        context.RolePermissions.RemoveRange(existingAssignments);

        foreach (var permission in permissions)
        {
            context.RolePermissions.Add(RolePermission.Create(roleId, permission.Id));
        }

        await context.SaveChangesAsync(cancellationToken);
        await permissionResolver.InvalidateRoleAsync(roleId, cancellationToken);

        var assignedPermissionCodes = await permissionResolver.GetPermissionCodesForRoleAsync(roleId, cancellationToken);

        return Result<RolePermissionsDto>.Success(new RolePermissionsDto
        {
            RoleName = roleName,
            AssignedPermissionCodes = assignedPermissionCodes
        });
    }
}
