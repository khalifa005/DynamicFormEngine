using System.Security.Claims;
using KH.Application.Auth.Commands.ExchangeSsoCode;
using KH.Application.Auth.Commands.Logout;
using KH.Application.Auth.Commands.SsoSignIn;
using KH.Application.Auth.Models;
using KH.Application.Auth.Queries.GetSsoStatus;
using KH.Domain.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Shared.Core.Common;
using Shared.Core.Options;

namespace MK.FormEngine.API.Controllers;

/// <summary>
/// The corporate SSO (SAML2) sign-in flow.
///
/// Kept apart from <see cref="AuthController"/> because most of these actions answer with a browser
/// redirect rather than a <c>Result</c> envelope — they sit in the middle of a navigation, not an
/// API call. The assertion consumer endpoint is not here: the SAML handler owns
/// <c>{Sso:ModulePath}/Acs</c> directly, which is what keeps signature validation in the library
/// rather than in hand-written XML parsing.
/// </summary>
[Route("api/v1/auth/sso")]
[AllowAnonymous]
public sealed class AuthSsoController(IOptions<SsoSettings> ssoOptions) : ApiControllerBase
{
    private const string SessionIndexClaimType = "SessionIndex";
    private const string SustainsysSessionIndexClaimType = "urn:Sustainsys.Saml2:SessionIndex";

    private SsoSettings Settings => ssoOptions.Value;

    /// <summary>
    /// Whether SSO is on, so the sign-in page knows what to render. Anonymous and deliberately
    /// content-free beyond the two switches — it is read before anyone has signed in.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(Result<SsoStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetSsoStatusQuery(), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Starts the sign-in by challenging the identity provider. A full-page browser navigation, not
    /// an XHR — the response is a redirect to OAM.
    /// </summary>
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        if (!Settings.Enabled)
        {
            return NotFound();
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = AppendReturnUrl(SsoDefaults.CallbackPath, SanitizeReturnUrl(returnUrl)),
        };

        return Challenge(properties, SsoDefaults.Saml2Scheme);
    }

    /// <summary>
    /// Where the SAML handler drops the browser once it has validated the assertion. Reads the
    /// established identity out of the holding cookie, decides whether FSMS admits it, then sends the
    /// browser back to the client app — with a single-use code on success, or a reason on refusal.
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? returnUrl, CancellationToken cancellationToken)
    {
        if (!Settings.Enabled)
        {
            return NotFound();
        }

        var authentication = await HttpContext.AuthenticateAsync(SsoDefaults.TempCookieScheme);

        // The holding cookie is spent either way: it exists for this one read.
        await HttpContext.SignOutAsync(SsoDefaults.TempCookieScheme);

        if (!authentication.Succeeded || authentication.Principal is null)
        {
            return RedirectToAccessDenied(SsoDefaults.DenialReasons.NoNameId, username: null);
        }

        var nameId = ResolveNameId(authentication.Principal);

        if (string.IsNullOrWhiteSpace(nameId))
        {
            return RedirectToAccessDenied(SsoDefaults.DenialReasons.NoNameId, username: null);
        }

        var result = await Mediator.Send(
            new SsoSignInCommand { NameId = nameId, SessionIndex = ResolveSessionIndex(authentication.Principal) },
            cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            return RedirectToAccessDenied(SsoDefaults.DenialReasons.ExchangeFailed, nameId);
        }

        if (!result.Data.IsGranted)
        {
            return RedirectToAccessDenied(result.Data.DenialReason!, nameId);
        }

        var target = AppendReturnUrl(
            $"{Settings.ClientAppBaseUrl.TrimEnd('/')}{SsoDefaults.ClientCallbackPath}" +
            $"?code={Uri.EscapeDataString(result.Data.AuthorizationCode!)}",
            SanitizeReturnUrl(returnUrl));

        return Redirect(target);
    }

    /// <summary>
    /// Trades the code from the callback for a session. This is the only part of the flow the client
    /// app calls directly, and it answers with the same payload as a password login.
    /// </summary>
    [HttpPost("exchange")]
    [ProducesResponseType(typeof(Result<AuthTokenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Exchange(
        [FromBody] ExchangeSsoCodeCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Ends the session here and at the identity provider. Revokes the caller's refresh tokens first
    /// when the request still carries a valid bearer, so signing out of OAM does not leave a live
    /// refresh token behind.
    /// </summary>
    [HttpGet("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!Settings.Enabled)
        {
            return NotFound();
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            await Mediator.Send(new LogoutCommand(), cancellationToken);
        }

        await HttpContext.SignOutAsync(SsoDefaults.TempCookieScheme);

        return string.IsNullOrWhiteSpace(Settings.LogoutUrl)
            ? Redirect($"{Settings.ClientAppBaseUrl.TrimEnd('/')}/")
            : Redirect(Settings.LogoutUrl);
    }

    /// <summary>
    /// The corporate user name from the assertion. Sustainsys maps the SAML NameID onto
    /// <see cref="ClaimTypes.NameIdentifier"/>; <see cref="ClaimTypes.Name"/> is the fallback for an
    /// identity provider that only sends a name claim.
    /// </summary>
    private static string? ResolveNameId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue(ClaimTypes.Name);

    private static string? ResolveSessionIndex(ClaimsPrincipal principal) =>
        principal.FindFirstValue(SustainsysSessionIndexClaimType)
        ?? principal.FindFirstValue(SessionIndexClaimType);

    /// <summary>
    /// Keeps the round trip from being turned into an open redirect: only a path inside the client
    /// app survives, never an absolute URL or a protocol-relative one.
    /// </summary>
    private static string? SanitizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        return returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            ? returnUrl
            : null;
    }

    private IActionResult RedirectToAccessDenied(string reason, string? username)
    {
        var target = $"{Settings.ClientAppBaseUrl.TrimEnd('/')}{SsoDefaults.ClientAccessDeniedPath}" +
                     $"?reason={Uri.EscapeDataString(reason)}";

        if (!string.IsNullOrWhiteSpace(username))
        {
            target += $"&username={Uri.EscapeDataString(username)}";
        }

        return Redirect(target);
    }

    private static string AppendReturnUrl(string url, string? returnUrl) =>
        string.IsNullOrWhiteSpace(returnUrl)
            ? url
            : $"{url}{(url.Contains('?') ? '&' : '?')}returnUrl={Uri.EscapeDataString(returnUrl)}";
}
