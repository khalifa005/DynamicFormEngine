using KH.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Core.Common;
using Shared.Core.Options;

namespace MK.FormEngine.API.Filters;

/// <summary>
/// Requires a valid <c>X-Api-Key</c> for this controller or action.
/// Resolves the caller from <c>ExternalConsumers</c> configuration and populates
/// <see cref="IExternalConsumerContext"/> via <c>HttpContext.Items</c>.
/// Apply only on endpoints that must be protected with API key — not on JWT or public routes.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireApiKeyAttribute : Attribute, IAuthorizationFilter
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

        var keySent = context.HttpContext.Request.Headers.TryGetValue(ApiKeyAuthentication.HeaderName, out var apiKeyHeader) &&
                      !string.IsNullOrWhiteSpace(apiKeyHeader);

        var message = keySent
            ? "Authentication failed. The API key is not valid for any registered application."
            : "Authentication failed. A valid X-Api-Key header is required.";

        var result = Result<object>.Fail(message, ApiErrorCodes.AuthenticationFailed, httpStatusCode: 401);
        context.Result = new JsonResult(result) { StatusCode = StatusCodes.Status401Unauthorized };
    }
}
