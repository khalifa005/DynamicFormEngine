public class Result<T>
{
    public bool IsSuccess { get; private set; } = false;
    public T? Data { get; private set; }
    public List<ErrorInfo> Errors { get; private set; } = [];
    public string? CorrelationId { get; private set; }

    // Success Constructor
    private Result(T? data, string? correlationId = null)
    {
        IsSuccess = true;
        Data = data;
        CorrelationId = correlationId ?? GetCorrelationIdFromContext();
    }

    // Error Constructor
    private Result(List<ErrorInfo> errors, string? correlationId = null)
    {
        IsSuccess = false;
        Errors = errors;
        CorrelationId = correlationId ?? GetCorrelationIdFromContext();
    }

    private Result(ErrorInfo error, string? correlationId = null)
    {
        IsSuccess = false;
        Errors.Add(error);
        CorrelationId = correlationId ?? GetCorrelationIdFromContext();
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
            // Fallback for non-HTTP context execution (e.g. testing, background threads)
        }
        return null;
    }

    // Success Helper
    public static Result<T> Success(T? data, string? correlationId = null) => new Result<T>(data, correlationId);

    // Error Helpers
    public static Result<T> Fail(string message, string? code = null, string? correlationId = null, int? httpStatusCode = null)
        => new Result<T>(new ErrorInfo(message, code, httpStatusCode), correlationId);

    public static Result<T> Fail(
        string message,
        string? code,
        int? httpStatusCode,
        string? ivrMessageEn,
        string? ivrMessageAr,
        string? correlationId = null)
        => new Result<T>(new ErrorInfo(message, code, httpStatusCode, ivrMessageEn, ivrMessageAr), correlationId);

    public static Result<T> Fail(ErrorInfo error, string? correlationId = null) => new(error, correlationId);
    public static Result<T> Fail(List<ErrorInfo> errors, string? correlationId = null) => new(errors, correlationId);


}

public class ErrorInfo(string? message, string? code = null, int? httpStatusCode = null, string? ivrMessageEn = null, string? ivrMessageAr = null)
{
    public string? Code { get; private set; } = code;
    public string? Message { get; private set; } = message;
    /// <summary>HTTP status that aligns with <see cref="Code"/> (also sent on the response line when applicable).</summary>
    public int? HttpStatusCode { get; private set; } = httpStatusCode;
    public string? IvrMessageEn { get; private set; } = ivrMessageEn;
    public string? IvrMessageAr { get; private set; } = ivrMessageAr;
}
