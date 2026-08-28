using Microsoft.Extensions.Logging;

namespace KH.Application.Common;

public static class ModuleLogScope
{
    public static IDisposable Begin(string module, ILogger logger) =>
        logger.BeginScope(new Dictionary<string, object> { ["Feature"] = module });
}
