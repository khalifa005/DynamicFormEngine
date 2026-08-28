using System.Security.Claims;
using KH.Application.Common.Interfaces;
using KH.Domain.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace KH.Infrastructure.Identity;

public sealed class ApplicationUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IPermissionResolver permissionResolver,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var permissions = await permissionResolver.GetPermissionCodesForUserAsync(user.Id);

        foreach (var permission in permissions)
        {
            identity.AddClaim(new Claim(PermissionClaimTypes.Permission, permission));
        }

        return identity;
    }
}
