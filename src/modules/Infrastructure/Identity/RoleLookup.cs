using KH.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KH.Infrastructure.Identity;

public sealed class RoleLookup(RoleManager<IdentityRole> roleManager) : IRoleLookup
{
    public async Task<string?> GetRoleIdByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByNameAsync(roleName);
        return role?.Id;
    }

    public async Task<bool> RoleExistsAsync(string roleName, CancellationToken cancellationToken = default) =>
        await roleManager.RoleExistsAsync(roleName);

    public async Task<IReadOnlyList<RoleSummary>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        await roleManager.Roles
            .AsNoTracking()
            .Where(r => r.Name != null)
            .OrderBy(r => r.Name)
            .Select(r => new RoleSummary(r.Id, r.Name!))
            .ToListAsync(cancellationToken);
}
