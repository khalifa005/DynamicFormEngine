using System.Security.Claims;
using KH.Application.Common.Interfaces;
using KH.Domain.Constants;

namespace MK.FormEngine.API.Services;

public class CurrentUser : IUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? Id => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName => User?.FindFirstValue(ClaimTypes.Name);

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public IReadOnlyList<string> Permissions =>
        User?.FindAll(PermissionClaimTypes.Permission).Select(c => c.Value).ToList() ?? [];

    public long? FieldTeamId =>
        long.TryParse(User?.FindFirstValue(AppClaimTypes.FieldTeamId), out var id) ? id : null;

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;

    public bool HasPermission(string permissionCode) =>
        User?.HasClaim(PermissionClaimTypes.Permission, permissionCode) ?? false;
}
