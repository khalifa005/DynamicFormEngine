using KH.Application.Permissions.Commands.AssignRolePermissions;
using KH.Application.Permissions.Models;
using KH.Application.Permissions.Queries.GetPermissions;
using KH.Application.Permissions.Queries.GetRolePermissions;
using KH.Application.Permissions.Queries.GetRoles;
using KH.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.Common;

namespace MK.FormEngine.API.Controllers;

[Authorize]
[Route("api/v1/admin")]
public sealed class AdminController : ApiControllerBase
{
    [HttpGet("permissions")]
    [Authorize(Policy = Policies.CanManageRolePermissions)]
    [ProducesResponseType(typeof(Result<IReadOnlyList<PermissionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPermissions(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetPermissionsQuery(), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Every role with the permissions it holds — the whole matrix in one call.</summary>
    [HttpGet("roles")]
    [Authorize(Policy = Policies.CanManageRolePermissions)]
    [ProducesResponseType(typeof(Result<IReadOnlyList<RoleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetRolesQuery(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("roles/{roleName}/permissions")]
    [Authorize(Policy = Policies.CanManageRolePermissions)]
    [ProducesResponseType(typeof(Result<RolePermissionsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRolePermissions(string roleName, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetRolePermissionsQuery { RoleName = roleName }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("roles/{roleName}/permissions")]
    [Authorize(Policy = Policies.CanManageRolePermissions)]
    [ProducesResponseType(typeof(Result<RolePermissionsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignRolePermissions(
        string roleName,
        [FromBody] AssignRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new AssignRolePermissionsCommand
            {
                RoleName = roleName,
                PermissionCodes = request.PermissionCodes
            },
            cancellationToken);

        return ToActionResult(result);
    }
}

public sealed class AssignRolePermissionsRequest
{
    public IReadOnlyList<string> PermissionCodes { get; init; } = [];
}
