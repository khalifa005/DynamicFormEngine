using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KH.Domain.Constants;
using KH.Infrastructure.Identity;
using Microsoft.IdentityModel.Tokens;
using Shared.Core.Options;

namespace KH.Infrastructure.Identity;

internal static class JwtTokenGenerator
{
    public static string GenerateAccessToken(
        ApplicationUser user,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions,
        JwtSettings settings,
        DateTime utcNow)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? user.Email ?? user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        // A crew account carries its team, so the mobile app never has to be told which crew the
        // token belongs to.
        if (user.FieldTeamId is long fieldTeamId)
        {
            claims.Add(new Claim(AppClaimTypes.FieldTeamId, fieldTeamId.ToString(CultureInfo.InvariantCulture)));

            if (!string.IsNullOrWhiteSpace(user.UserName))
            {
                claims.Add(new Claim(AppClaimTypes.UserCode, user.UserName));
            }
        }

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(permissions.Select(permission => new Claim(PermissionClaimTypes.Permission, permission)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var expiresAt = utcNow.AddMinutes(settings.ExpiryMinutes);

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: utcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
