using System;

namespace Shared.Core.Entities;

/// <summary>
/// Represents a plain, serialization-safe database entity and POCO for storing request-response audit log details.
/// This class contains no references to HttpContext or other non-serializable objects.
/// </summary>
public class AuditEntry
{
    /// <summary>
    /// Unique identifier for the audit log record.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Correlation ID associated with the logical request/response flow.
    /// Used for end-to-end trace mapping.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Type of caller: User, Application, or Anonymous.
    /// </summary>
    public string CallerType { get; set; } = string.Empty;

    /// <summary>
    /// The unique identifier of the user (e.g. from JWT claims), if applicable.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// The name of the calling application (e.g. from client header), if applicable.
    /// </summary>
    public string? AppName { get; set; }

    /// <summary>
    /// The client IP address that initiated the request.
    /// </summary>
    public string ClientIp { get; set; } = string.Empty;

    /// <summary>
    /// The API endpoint / route path requested.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// The HTTP method of the request (GET, POST, PUT, DELETE, etc.).
    /// </summary>
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>
    /// The serialized request body payload. Nullable for requests without bodies.
    /// </summary>
    public string? RequestBody { get; set; }

    /// <summary>
    /// The serialized response body or result object. Nullable for void responses or large files.
    /// </summary>
    public string? ResponseBody { get; set; }

    /// <summary>
    /// The HTTP status code returned to the client (e.g., 200, 400, 500).
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Indicates whether the request was completed successfully (typically 2xx status codes).
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Total duration of the request execution in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Optional semantic name of the audit event defined at the endpoint level.
    /// </summary>
    public string? EventName { get; set; }

    /// <summary>
    /// The timestamp when the request was received by the server.
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// The timestamp when the response was returned by the server.
    /// </summary>
    public DateTime RespondedAt { get; set; }
}
