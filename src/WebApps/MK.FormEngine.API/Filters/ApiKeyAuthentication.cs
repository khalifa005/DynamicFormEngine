using Shared.Core.Options;

namespace MK.FormEngine.API.Filters;

internal static class ApiKeyAuthentication
{
    public const string HeaderName = "X-Api-Key";

    public static bool TryAuthenticate(HttpContext httpContext, ExternalConsumersOptions consumers)
    {
        if (httpContext.Items.ContainsKey("AppId"))
        {
            return true;
        }

        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var apiKeyHeader) ||
            string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            return false;
        }

        if (!consumers.TryResolve(apiKeyHeader.ToString(), out var systemName, out var app))
        {
            return false;
        }

        SetConsumerContext(httpContext, systemName!, app!);
        return true;
    }

    private static void SetConsumerContext(HttpContext httpContext, string systemName, ExternalConsumerAppOptions app)
    {
        httpContext.Items["AppId"] = app.AppID;
        httpContext.Items["AppName"] = app.AppName;
        httpContext.Items["SystemName"] = systemName;
        httpContext.Items["CallerID"] = app.CallerID;
        httpContext.Items["AllowTwoWaySession"] = app.AllowTwoWaySession;
        httpContext.Items["MaxActiveSessionPerAppAndTask"] = app.MaxActiveSessionPerAppAndTask;
        httpContext.Items["MaxActiveSessionPerSourceUser"] = app.MaxActiveSessionPerSourceUser;
        httpContext.Items["MaxActiveSessionPerTargetUser"] = app.MaxActiveSessionPerTargetUser;
        httpContext.Items["DuplicateSessionPolicy"] = app.DuplicateSessionPolicy;
        var sessionSelection = app.ResolveIvrMessage(Shared.Core.Enums.IvrMessageScenarioEnum.SessionSelection);
        httpContext.Items["IvrMessageEn"] = sessionSelection.En;
        httpContext.Items["IvrMessageAr"] = sessionSelection.Ar;
        httpContext.Items["ExternalConsumerApp"] = app;
    }
}
