using KH.Application.Common.Interfaces;
using KH.Domain.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Core.Common;
using Shared.Core.Options;

namespace MK.FormEngine.API.Filters;

/// <summary>
/// Allows report access via valid <c>X-Api-Key</c> (scoped to caller app) or JWT administrator role.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireApiKeyOrAdministratorAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var consumers = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<ExternalConsumersOptions>>()
            .Value;

        if (ApiKeyAuthentication.TryAuthenticate(context.HttpContext, consumers))
        {
            return;
        }

        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated == true && user.IsInRole(Roles.Administrator))
        {
            return;
        }

        var keySent = context.HttpContext.Request.Headers.TryGetValue(ApiKeyAuthentication.HeaderName, out var apiKeyHeader) &&
                      !string.IsNullOrWhiteSpace(apiKeyHeader);

        var message = keySent
            ? "Authentication failed. The API key is not valid for any registered application."
            : "Authentication failed. A valid X-Api-Key or administrator JWT is required.";

        var result = Result<object>.Fail(message, ApiErrorCodes.AuthenticationFailed, httpStatusCode: 401);
        context.Result = new JsonResult(result) { StatusCode = StatusCodes.Status401Unauthorized };
    }
}
