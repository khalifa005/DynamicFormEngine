using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KH.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KH.Application.Common.Behaviours;

public class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger;
    private readonly IUser _user;

    public LoggingBehaviour(ILogger<TRequest> logger, IUser user)
    {
        _logger = logger;
        _user = user;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        
        // Extract a clean feature name (e.g., GetTodoItemsWithPaginationQuery -> GetTodoItemsWithPagination)
        var featureName = requestName;
        if (featureName.EndsWith("Command", StringComparison.OrdinalIgnoreCase))
        {
            featureName = featureName.Substring(0, featureName.Length - "Command".Length);
        }
        else if (featureName.EndsWith("Query", StringComparison.OrdinalIgnoreCase))
        {
            featureName = featureName.Substring(0, featureName.Length - "Query".Length);
        }

        var userId = _user.Id ?? string.Empty;
        var userName = _user.UserName ?? string.Empty;

        // Push Feature into log scope (Serilog translates this into LogContext automatically)
        var scopeState = new Dictionary<string, object>
        {
            ["Feature"] = featureName
        };

        using (_logger.BeginScope(scopeState))
        {
            _logger.LogInformation("KH Request: {Name} {@UserId} {@UserName} {@Request}",
                requestName, userId, userName, request);

            return await next();
        }
    }
}
