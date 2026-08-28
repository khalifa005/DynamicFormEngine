using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Shared.Logs.Audit;

/// <summary>
/// Production-ready service that resolves caller identities (User, Application, or Anonymous) 
/// based on the current HttpContext.
/// </summary>
public class CallerResolver : ICallerResolver
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CallerResolver(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Evaluates headers and security context to identify the caller type and identifier.
    /// </summary>
    public (string CallerType, string? UserId, string? AppName) ResolveCaller()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return ("Anonymous", null, null);
        }

        // 1. Resolve User from JWT claims (NameIdentifier or sub)
        var user = context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                         ?? user.FindFirst("sub")?.Value;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                return ("User", userId, null);
            }
        }

        // 2. Resolve Application from X-App-Name header
        if (context.Request.Headers.TryGetValue("X-App-Name", out var appNameHeader) && 
            !string.IsNullOrWhiteSpace(appNameHeader))
        {
            // Note: We only capture the resolved application name. Any API Key or secret headers are strictly ignored.
            return ("Application", null, appNameHeader.ToString());
        }

        // 3. Fallback to Anonymous
        return ("Anonymous", null, null);
    }
}
