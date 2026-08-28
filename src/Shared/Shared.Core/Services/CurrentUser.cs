using Microsoft.AspNetCore.Http;
using Shared.Core.Interfaces;

namespace Shared.Core.Services;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    //public string? UserId => httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier);
    //public string? Email => httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);
    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public string[] Roles => throw new NotImplementedException();

    public string ClientIp => throw new NotImplementedException();

    public string SamlNameId => throw new NotImplementedException();

    public string? Username => throw new NotImplementedException();

    public string? UserId => throw new NotImplementedException();

    public string? Email => throw new NotImplementedException();
}
