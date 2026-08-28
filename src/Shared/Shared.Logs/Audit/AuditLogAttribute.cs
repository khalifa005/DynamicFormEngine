using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Hangfire;
using Shared.Core.Entities;

namespace Shared.Logs.Audit;

/// <summary>
/// A high-performance Action Filter Attribute applied to endpoints to capture 
/// comprehensive request-response audit trails and enqueue them for background persistence.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class AuditLogAttribute : ActionFilterAttribute
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string StopwatchKey = "Audit_Stopwatch";
    private const string RequestedAtKey = "Audit_RequestedAt";

    /// <summary>
    /// Optional semantic labeling for the audit event.
    /// </summary>
    public string? EventName { get; set; }

    /// <summary>
    /// Executes early in the request execution pipeline before the endpoint action runs.
    /// Starts the stopwatch and captures the initial request timestamp.
    /// </summary>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // NOTE: Action Filter instances are treated as singletons by ASP.NET Core when applied directly as attributes.
        // Therefore, request-specific state must NEVER be stored in instance fields. 
        // We safely store our timing state inside HttpContext.Items.
        context.HttpContext.Items[StopwatchKey] = Stopwatch.StartNew();
        context.HttpContext.Items[RequestedAtKey] = DateTime.UtcNow;
        context.HttpContext.Items["Audit_ActionArguments"] = context.ActionArguments;

        base.OnActionExecuting(context);
    }

    /// <summary>
    /// Executes after the endpoint action completes and the response is formulated.
    /// Builds the serializable AuditEntry, handles caller identification, and enqueues the Hangfire job.
    /// </summary>
    public override void OnActionExecuted(ActionExecutedContext context)
    {
        base.OnActionExecuted(context);

        var httpContext = context.HttpContext;
        var services = httpContext.RequestServices;

        // Retrieve the correlation ID generated early in the pipeline by CorrelationMiddleware
        var correlationId = httpContext.Items.TryGetValue(Middleware.CorrelationMiddleware.CorrelationContextItemKey, out var corrId)
            ? corrId?.ToString() ?? Guid.NewGuid().ToString()
            : Guid.NewGuid().ToString();

        var logger = services.GetRequiredService<ILogger<AuditLogAttribute>>();

        try
        {
            // 1. Calculate timing details safely
            var requestedAt = httpContext.Items.TryGetValue(RequestedAtKey, out var reqAt) && reqAt is DateTime time 
                ? time 
                : DateTime.UtcNow;

            long durationMs = 0;
            if (httpContext.Items.TryGetValue(StopwatchKey, out var swObj) && swObj is Stopwatch sw)
            {
                sw.Stop();
                durationMs = sw.ElapsedMilliseconds;
            }

            var respondedAt = DateTime.UtcNow;

            // 2. Resolve the identity of the caller (User, Application, or Anonymous) using the dedicated resolver
            var callerResolver = services.GetRequiredService<ICallerResolver>();
            var (callerType, userId, appName) = callerResolver.ResolveCaller();

            // 3. Capture client connection and HTTP request details
            var clientIp = GetClientIp(httpContext);
            var endpoint = httpContext.Request.Path.Value ?? "/";
            var httpMethod = httpContext.Request.Method;

            // 4. Safely serialize request arguments (payload)
            var actionArguments = httpContext.Items.TryGetValue("Audit_ActionArguments", out var argsObj) && 
                                  argsObj is System.Collections.Generic.IDictionary<string, object?> args
                ? args
                : null;
            var requestBody = SerializeArguments(actionArguments, logger);

            // 5. Capture status code and response payload details
            var (statusCode, responseBody) = ParseResponse(context.Result, httpContext, logger);
            var isSuccess = statusCode >= 200 && statusCode < 300;

            // 6. Build the plain serializable POCO (No HttpContext references, entirely serialization-safe)
            var auditEntry = new AuditEntry
            {
                CorrelationId = correlationId,
                CallerType = callerType,
                UserId = userId,
                AppName = appName,
                ClientIp = clientIp,
                Endpoint = endpoint,
                HttpMethod = httpMethod,
                RequestBody = requestBody,
                ResponseBody = responseBody,
                StatusCode = statusCode,
                IsSuccess = isSuccess,
                DurationMs = durationMs,
                EventName = EventName,
                RequestedAt = requestedAt,
                RespondedAt = respondedAt
            };

            // 7. Fire-and-forget: Enqueue the persistence job via the injected IBackgroundJobClient (NOT static Hangfire class)
            var jobClient = services.GetService<IBackgroundJobClient>();
            if (jobClient != null)
            {
                jobClient.Enqueue<IAuditService>(service => service.SaveAsync(auditEntry));
            }
            else
            {
                // Fallback: If Hangfire is disabled by configuration, persist audit entry asynchronously via Task.Run
                logger.LogWarning("Hangfire background job client is not registered (disabled in configuration). Persisting audit entry asynchronously. CorrelationId: {CorrelationId}", correlationId);
                var auditService = services.GetRequiredService<IAuditService>();
                System.Threading.Tasks.Task.Run(() => auditService.SaveAsync(auditEntry));
            }
        }
        catch (Exception ex)
        {
            // CRITICAL ERROR SAFETY: Never let audit logging failures propagate to the client 
            // and disrupt primary business operations.
            logger.LogWarning(ex, "Failed to enqueue background audit logging job. CorrelationId: {CorrelationId}", correlationId);
        }
    }

    /// <summary>
    /// Safely serializes the dictionary of action arguments to JSON.
    /// Skips framework parameters (e.g. CancellationToken) and serializes each value by its runtime type.
    /// </summary>
    private static string? SerializeArguments(IDictionary<string, object?>? arguments, ILogger logger)
    {
        if (arguments == null || arguments.Count == 0)
        {
            return null;
        }

        try
        {
            var serialized = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

            foreach (var (key, value) in arguments)
            {
                if (value is null || ShouldSkipArgument(value))
                {
                    continue;
                }

                serialized[key] = JsonSerializer.SerializeToElement(value, value.GetType(), SerializerOptions);
            }

            if (serialized.Count == 0)
            {
                return null;
            }

            // Single bound argument (typical [FromBody] POST) — log the payload directly, not {"command": {...}}.
            if (serialized.Count == 1)
            {
                return serialized.Values.First().GetRawText();
            }

            return JsonSerializer.Serialize(serialized, SerializerOptions);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to serialize request action arguments for auditing.");
            return "[Serialization Failed]";
        }
    }

    /// <summary>
    /// Framework and infrastructure parameters that must not appear in audit request bodies.
    /// </summary>
    private static bool ShouldSkipArgument(object value) =>
        value switch
        {
            CancellationToken => true,
            IFormFile => true,
            Stream => true,
            HttpContext => true,
            HttpRequest => true,
            HttpResponse => true,
            ClaimsPrincipal => true,
            _ => false
        };

    /// <summary>
    /// Identifies and formats the client IP address. Supports resolution behind standard proxies.
    /// </summary>
    private static string GetClientIp(HttpContext context)
    {
        // Check X-Forwarded-For header in case of load balancers / reverse proxies
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedHeader) && 
            !string.IsNullOrWhiteSpace(forwardedHeader))
        {
            var firstIp = forwardedHeader.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(firstIp))
            {
                return firstIp;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
    }

    /// <summary>
    /// Extracts the HTTP status code and serializable content from the MVC IActionResult result.
    /// </summary>
    private static (int StatusCode, string? Content) ParseResponse(IActionResult? result, HttpContext context, ILogger logger)
    {
        if (result == null)
        {
            return (context.Response.StatusCode, null);
        }

        int statusCode = context.Response.StatusCode;
        string? serializedContent = null;

        try
        {
            switch (result)
            {
                case ObjectResult objectResult:
                    statusCode = objectResult.StatusCode ?? statusCode;
                    if (objectResult.Value != null)
                    {
                        serializedContent = JsonSerializer.Serialize(objectResult.Value, SerializerOptions);
                    }
                    break;

                case JsonResult jsonResult:
                    statusCode = jsonResult.StatusCode ?? statusCode;
                    if (jsonResult.Value != null)
                    {
                        serializedContent = JsonSerializer.Serialize(jsonResult.Value, SerializerOptions);
                    }
                    break;

                case ContentResult contentResult:
                    statusCode = contentResult.StatusCode ?? statusCode;
                    serializedContent = contentResult.Content;
                    break;

                case StatusCodeResult statusCodeResult:
                    statusCode = statusCodeResult.StatusCode;
                    break;

                default:
                    // Fallback for Redirects, FileResults, ViewResults, etc.
                    serializedContent = $"[Result Type: {result.GetType().Name}]";
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to serialize controller response action result.");
            serializedContent = "[Response Serialization Failed]";
        }

        return (statusCode, serializedContent);
    }
}
