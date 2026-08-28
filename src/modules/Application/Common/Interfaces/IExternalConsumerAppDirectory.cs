using Shared.Core.Options;

namespace KH.Application.Common.Interfaces;

public interface IExternalConsumerAppDirectory
{
    ExternalConsumerAppOptions? FindByAppId(string? appId);
}
