using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Shared.Logs.Middleware;

/// <summary>
/// A lightweight middleware registered early in the ASP.NET Core pipeline.
/// It extracts or generates a unique correlation ID used to trace a request end-to-end.
/// </summary>
public class CorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationHeaderKey = "X-Correlation-ID";
    public const string CorrelationContextItemKey = "CorrelationId";

    public CorrelationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Checks for an incoming X-Correlation-ID, generates a new Guid if missing,
    /// sets it in HttpContext.Items, and appends it to the outgoing response headers.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Try to extract correlation ID from request headers
        if (!context.Request.Headers.TryGetValue(CorrelationHeaderKey, out var correlationId) || 
            string.IsNullOrWhiteSpace(correlationId))
        {
            // 2. Generate a new unique Guid if absent
            correlationId = Guid.NewGuid().ToString();
        }

        // 3. Store in HttpContext.Items for downstream access in filters, handlers, etc.
        context.Items[CorrelationContextItemKey] = correlationId.ToString();

        // 4. Return it to the client in the response header as X-Correlation-ID
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationHeaderKey))
            {
                context.Response.Headers.Append(CorrelationHeaderKey, correlationId);
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
