using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Shared.Core.Common;
using Shared.Core.Exceptions;
using Shared.Core.Interfaces;
using Shared.Core.Resources;

namespace Shared.Core.Filters;

public class GlobalExceptionHandlingFilter : IAsyncExceptionFilter
{
    private readonly ILogger<GlobalExceptionHandlingFilter> _logger;
    private readonly IStringLocalizer<AppResource> _localizer;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly IWebHostEnvironment _env;

    public GlobalExceptionHandlingFilter(
        ILogger<GlobalExceptionHandlingFilter> logger,
        IStringLocalizer<AppResource> localizer,
        ICorrelationIdProvider correlationIdProvider,
        IWebHostEnvironment env)
    {
        _logger = logger;
        _localizer = localizer;
        _correlationIdProvider = correlationIdProvider;
        _env = env;
    }

    public async Task OnExceptionAsync(ExceptionContext context)
    {
        var exception = context.Exception;
        Result<object> response = default!;

        if (exception is ApiException apiException)
        {
            var status = (int)apiException.StatusCode;
            context.HttpContext.Response.StatusCode = status;
            response = Result<object>.Fail(apiException.Message, apiException.StatusCode.ToString(), _correlationIdProvider.Get(), status);

            _logger.LogWarning(apiException, "API Exception occurred: {Message}", apiException.Message);
        }
        else if (exception is UserFriendlyException friendlyException)
        {
            context.HttpContext.Response.StatusCode = 400;
            var localizedMessage = _localizer[friendlyException.Code ?? friendlyException.Message];

            response = Result<object>.Fail(localizedMessage, friendlyException.Code, _correlationIdProvider.Get(), 400);

            _logger.LogWarning(friendlyException, "Friendly Exception occurred: {Message}", localizedMessage);
        }
        else if (exception is BusinessException abpException)
        {
            var statusCode = ApiErrorHttpMapper.GetHttpStatusCode(abpException.Code);
            context.HttpContext.Response.StatusCode = statusCode;
            response = Result<object>.Fail(abpException.Message, abpException.Code, _correlationIdProvider.Get(), statusCode);

            _logger.LogWarning(abpException, "API Exception occurred: {Message}", abpException.Message);
        }
        else if (exception is FluentValidation.ValidationException validationException)
        {
            static bool IsApiStyleErrorCode(string? c)
            {
                if (string.IsNullOrEmpty(c) || c[0] != 'E')
                {
                    return false;
                }

                for (var i = 1; i < c.Length; i++)
                {
                    if (!char.IsDigit(c[i]))
                    {
                        return false;
                    }
                }

                return c.Length >= 6;
            }

            var errors = validationException.Errors
                .Select(e =>
                {
                    var code = IsApiStyleErrorCode(e.ErrorCode)
                        ? e.ErrorCode!
                        : ApiErrorCodes.ValidationError;
                    var status = ApiErrorHttpMapper.GetHttpStatusCode(code);
                    return new ErrorInfo(string.Concat(e.ErrorMessage, " ", e.PropertyName), code, status);
                })
                .ToList();

            var responseStatus = errors.Count == 0
                ? 400
                : errors.Max(e => e.HttpStatusCode ?? 400);
            context.HttpContext.Response.StatusCode = responseStatus;
            response = Result<object>.Fail(errors, _correlationIdProvider.Get());

            _logger.LogWarning(validationException, "Validation Exception occurred");
        }
        else if (exception is DomainException domainException)
        {
            var code = domainException.Code ?? ApiErrorCodes.BackendBusinessRuleValidation;
            var statusCode = ApiErrorHttpMapper.GetHttpStatusCode(code);
            context.HttpContext.Response.StatusCode = statusCode;
            response = Result<object>.Fail(
                domainException.Message,
                code,
                _correlationIdProvider.Get(),
                statusCode);

            _logger.LogWarning(domainException, "Domain rule violation: {Message}", domainException.Message);
        }
        else if (exception is EntityNotFoundException entityNotFoundException)
        {
            context.HttpContext.Response.StatusCode = 404;
            response = Result<object>.Fail(
                entityNotFoundException.Message,
                ApiErrorCodes.NotFound,
                _correlationIdProvider.Get(),
                404);

            _logger.LogWarning("NotFound Exception occurred");
        }
        else
        {
            context.HttpContext.Response.StatusCode = 500;
            response = Result<object>.Fail(
                _env.IsDevelopment() ? exception.Message : _localizer[AppResource.GeneralErrorMessage],
                ApiErrorCodes.InternalServiceError,
                _correlationIdProvider.Get(),
                500);

            _logger.LogError(exception, "Unhandled Exception occurred: {Message}", exception.Message);
        }

        context.Result = new JsonResult(response)
        {
            StatusCode = context.HttpContext.Response.StatusCode
        };

        context.ExceptionHandled = true;

        await Task.CompletedTask;
    }
}
