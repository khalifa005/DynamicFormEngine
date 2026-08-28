using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Shared.Logs.Middleware;

public class TenantLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public TenantLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = GetTenantId(context);

        // Push TenantId into LogContext so all downstream logs automatically carry it
        using (LogContext.PushProperty("TenantId", tenantId))
        {
            await _next(context);
        }
    }

    private static string GetTenantId(HttpContext context)
    {
        // 1. Try from HTTP Header
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) && !string.IsNullOrWhiteSpace(tenantHeader))
        {
            return tenantHeader.ToString();
        }

        // 2. Try from Query String
        if (context.Request.Query.TryGetValue("tenantId", out var tenantQuery) && !string.IsNullOrWhiteSpace(tenantQuery))
        {
            return tenantQuery.ToString();
        }

        // 3. Try from User Claims (if authenticated)
        var user = context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = user.FindFirst("tenant_id") ?? user.FindFirst("TenantId") ?? user.FindFirst("tenant");
            if (tenantClaim != null && !string.IsNullOrWhiteSpace(tenantClaim.Value))
            {
                return tenantClaim.Value;
            }
        }

        // 4. Default fallback
        return "NWC";
    }
}
