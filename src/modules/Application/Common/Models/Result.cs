namespace KH.Application.Common.Models;

public class Result
{
    internal Result(bool succeeded, IEnumerable<string> errors, string? correlationId = null)
    {
        Succeeded = succeeded;
        Errors = errors.ToArray();
        CorrelationId = correlationId ?? GetCorrelationIdFromContext();
    }

    public bool Succeeded { get; init; }

    public string[] Errors { get; init; }

    public string? CorrelationId { get; private set; }

    public static Result Success(string? correlationId = null)
    {
        return new Result(true, Array.Empty<string>(), correlationId);
    }

    public static Result Failure(IEnumerable<string> errors, string? correlationId = null)
    {
        return new Result(false, errors, correlationId);
    }

    private static string? GetCorrelationIdFromContext()
    {
        try
        {
            var httpContext = new Microsoft.AspNetCore.Http.HttpContextAccessor().HttpContext;
            if (httpContext != null && httpContext.Items.TryGetValue("CorrelationId", out var idObj) && idObj is string id)
            {
                return id;
            }
        }
        catch
        {
            // Fallback for non-HTTP contexts (e.g. testing)
        }
        return null;
    }
}
