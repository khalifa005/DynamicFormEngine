using KH.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using Shared.Core.Options;

namespace MK.FormEngine.API.Services;

public sealed class ExternalConsumerAppDirectory(IOptions<ExternalConsumersOptions> options) : IExternalConsumerAppDirectory
{
    public ExternalConsumerAppOptions? FindByAppId(string? appId) =>
        options.Value.TryResolveByAppId(appId, out var app) ? app : null;
}
