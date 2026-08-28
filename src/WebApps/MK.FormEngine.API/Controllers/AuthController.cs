using KH.Application.Auth.Commands.Logout;
using KH.Application.Auth.Commands.LoginUser;
using KH.Application.Auth.Commands.RefreshAccessToken;
using KH.Application.Auth.Models;
using KH.Application.Auth.Queries.GetCurrentUserProfile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shared.Core.Common;

namespace MK.FormEngine.API.Controllers;

[Route("api/v1/auth")]
public sealed class AuthController : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Result<AuthTokenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginUserCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// OAuth2 password-grant shaped token endpoint for Swagger UI Authorize (username + password).
    /// Prefer <see cref="Login"/> from application clients.
    /// </summary>
    [HttpPost("token")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Token(
        [FromForm] string? username,
        [FromForm] string? password,
        [FromForm(Name = "grant_type")] string? grantType,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(grantType, "password", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(grantType))
        {
            return BadRequest(new { error = "unsupported_grant_type", error_description = "Only grant_type=password is supported." });
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new { error = "invalid_request", error_description = "username and password are required." });
        }

        var result = await Mediator.Send(
            new LoginUserCommand { UserName = username, Password = password },
            cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            return Unauthorized(new
            {
                error = "invalid_grant",
                error_description = result.Errors.FirstOrDefault()?.Message ?? "Invalid username or password."
            });
        }

        var token = result.Data;
        return Ok(new
        {
            access_token = token.AccessToken,
            token_type = "Bearer",
            expires_in = token.ExpiresInSeconds,
            refresh_token = token.RefreshToken
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Result<AuthTokenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh([FromBody] RefreshAccessTokenCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Ends the caller's session. Post the refresh token to close just that session, or an empty
    /// body to close every session the account holds. The access token itself is not recalled — the
    /// client drops its copy.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] LogoutCommand? command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command ?? new LogoutCommand(), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>The caller's own identity, roles/permissions and resolved territory.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(Result<CurrentUserProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetCurrentUserProfileQuery(), cancellationToken);
        return ToActionResult(result);
    }
}
