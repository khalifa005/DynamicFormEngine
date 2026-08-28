using KH.Application.Common.Interfaces;
using Shared.Core.Options;

namespace MK.FormEngine.API.Services;

public sealed class ExternalConsumerContext(IHttpContextAccessor httpContextAccessor) : IExternalConsumerContext
{
    public bool IsAuthenticated => httpContextAccessor.HttpContext?.Items.ContainsKey("AppId") == true;

    public string? AppId => httpContextAccessor.HttpContext?.Items["AppId"]?.ToString();

    public string? AppName => httpContextAccessor.HttpContext?.Items["AppName"]?.ToString();

    public string? SystemName => httpContextAccessor.HttpContext?.Items["SystemName"]?.ToString();

    public string? CallerId => httpContextAccessor.HttpContext?.Items["CallerID"]?.ToString();

    public bool AllowTwoWaySession => httpContextAccessor.HttpContext?.Items["AllowTwoWaySession"] is bool b && b;

    public int MaxActiveSessionPerAppAndTask =>
        httpContextAccessor.HttpContext?.Items["MaxActiveSessionPerAppAndTask"] is int i ? i : 1;

    public int MaxActiveSessionPerSourceUser =>
        httpContextAccessor.HttpContext?.Items["MaxActiveSessionPerSourceUser"] is int i ? i : 1;

    public int MaxActiveSessionPerTargetUser =>
        httpContextAccessor.HttpContext?.Items["MaxActiveSessionPerTargetUser"] is int i ? i : 1;

    public string DuplicateSessionPolicy =>
        httpContextAccessor.HttpContext?.Items["DuplicateSessionPolicy"]?.ToString() ?? "CloseAndReplace";

    public string IvrMessageEn =>
        httpContextAccessor.HttpContext?.Items["IvrMessageEn"]?.ToString() ?? "Please select your session.";

    public string IvrMessageAr =>
        httpContextAccessor.HttpContext?.Items["IvrMessageAr"]?.ToString() ?? "يرجى اختيار الجلسة.";

    public ExternalConsumerAppOptions? CurrentApp =>
        httpContextAccessor.HttpContext?.Items["ExternalConsumerApp"] as ExternalConsumerAppOptions;
}
