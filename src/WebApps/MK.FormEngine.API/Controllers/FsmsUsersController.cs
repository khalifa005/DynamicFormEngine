using KH.Application.Fsms.Users.Commands.CreateUser;
using KH.Application.Fsms.Users.Commands.ResetUserPassword;
using KH.Application.Fsms.Users.Commands.SetUserStatus;
using KH.Application.Fsms.Users.Commands.UpdateUser;
using KH.Application.Fsms.Users.Models;
using KH.Application.Fsms.Users.Queries.GetUserById;
using KH.Application.Fsms.Users.Queries.GetUsers;
using KH.Domain.Constants.Fsms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.Common;

namespace MK.FormEngine.API.Controllers;

/// <summary>Back-office account administration — list / create / edit / roles / territory / lockout / password.</summary>
[Authorize(Policy = FsmsPolicies.ManageUsers)]
[Route("api/v1/fsms/users")]
public sealed class FsmsUsersController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(Result<PagedResult<UserListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] GetUsersQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Result<UserDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserById(string id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetUserByIdQuery { UserId = id }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(Result<UserDetailDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Result<UserDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { UserId = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Enables or disables a login via lockout.</summary>
    [HttpPut("{id}/status")]
    [ProducesResponseType(typeof(Result<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStatus(string id, [FromBody] SetUserStatusCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { UserId = id }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{id}/reset-password")]
    [ProducesResponseType(typeof(Result<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword(string id, [FromBody] ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { UserId = id }, cancellationToken);
        return ToActionResult(result);
    }
}
