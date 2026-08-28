using Microsoft.AspNetCore.Http;
using Shared.Core.Interfaces;

namespace Shared.Core.Services;

public class CorrelationIdProvider : ICorrelationIdProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Get()
    {
        return _httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();
    }
}
