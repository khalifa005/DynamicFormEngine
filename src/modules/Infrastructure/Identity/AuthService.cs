using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KH.Application.Auth.Models;
using KH.Application.Common.Interfaces;
using KH.Application.Fsms.Common.Lookups;
using KH.Domain.Constants;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Infrastructure.Identity;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    IApplicationDbContext context,
    IPermissionResolver permissionResolver,
    IActiveDirectoryAuthenticator activeDirectory,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings,
    IOptions<JwtSettings> jwtOptions,
    IOptions<SsoSettings> ssoOptions,
    TimeProvider timeProvider) : IAuthService
{
    /// <summary>One message for every way a team sign-in can fail, so it never leaks which one it was.</summary>
    private const string InvalidTeamCredentials = "Invalid team code or password.";

    private const string TeamScopeRequired =
        "This field team has no territory scope assigned. Contact an administrator.";

    private const string UserScopeRequired =
        "This account has no territory scope assigned. Contact an administrator.";

    private const string SsoRequired = "Sign in through NWC SSO.";

    private const string TeamNoLongerActive = "The field team linked to this login is no longer active.";

    /// <summary>
    /// Said when the directory itself could not answer. Deliberately not a credentials message: the
    /// crew's password may be perfectly good, and telling them it is wrong sends them to reset a
    /// password that does not need resetting.
    /// </summary>
    private const string DirectoryUnavailable =
        "Sign-in is temporarily unavailable. Please try again shortly.";

    public async Task<Result<AuthTokenDto>> LoginAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByNameAsync(userName);

        if (user is null || !await userManager.CheckPasswordAsync(user, password))
        {
            return Result<AuthTokenDto>.Fail(
                "Invalid username or password.",
                ApiErrorCodes.AuthenticationFailed,
                httpStatusCode: 401);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Result<AuthTokenDto>.Fail(
                "User account is locked out or inactive.",
                ApiErrorCodes.AuthenticationFailed,
                httpStatusCode: 401);
        }

        // With corporate SSO on, the password door closes for the back office — but never for a crew
        // account, which has no OAM identity, and never for Administrator, which keeps a way in for
        // when OAM itself is unreachable.
        var ssoGate = await EnsureLocalLoginAllowedAsync(user);
        if (ssoGate is not null)
        {
            return ssoGate;
        }

        if (user.FieldTeamId is long fieldTeamId &&
            !await FieldTeamIsActiveAsync(fieldTeamId, cancellationToken))
        {
            return Result<AuthTokenDto>.Fail(
                TeamNoLongerActive,
                ApiErrorCodes.AuthenticationFailed,
                httpStatusCode: 401);
        }

        var scopeGate = await EnsureRequiredScopeAsync(user, cancellationToken);
        if (scopeGate is not null)
        {
            return scopeGate;
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    /// <summary>
    /// Turns a back-office account away from password login while SSO is enabled. Returns null when
    /// the account may still sign in locally: field-crew accounts always, Administrator when
    /// <see cref="SsoSettings.AllowLocalLoginForAdministrators"/> is set.
    /// </summary>
    private async Task<Result<AuthTokenDto>?> EnsureLocalLoginAllowedAsync(ApplicationUser user)
    {
        var sso = ssoOptions.Value;

        if (!sso.Enabled || user.FieldTeamId is not null)
        {
            return null;
        }

        if (sso.AllowLocalLoginForAdministrators &&
            await userManager.IsInRoleAsync(user, Roles.Administrator))
        {
            return null;
        }

        return Result<AuthTokenDto>.Fail(
            SsoRequired,
            ApiErrorCodes.AuthenticationFailed,
            httpStatusCode: 401);
    }

    public async Task<Result<AuthTokenDto>> LoginFieldTeamAsync(
        string userCode,
        string password,
        CancellationToken cancellationToken)
    {
        var trimmedUserCode = userCode.Trim();
        var user = await userManager.FindByNameAsync(trimmedUserCode);

        if (user is null)
        {
            return Result<AuthTokenDto>.Fail(
                InvalidTeamCredentials,
                ApiErrorCodes.AuthenticationFailed,
                httpStatusCode: 401);
        }

        var passwordGate = await VerifyFieldTeamPasswordAsync(user, trimmedUserCode, password, cancellationToken);
        if (passwordGate is not null)
        {
            return passwordGate;
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Result<AuthTokenDto>.Fail(
                TeamNoLongerActive,
                ApiErrorCodes.AuthenticationFailed,
                httpStatusCode: 401);
        }

        // The mobile door only opens for a crew account. Saying no more than "invalid credentials"
        // keeps a back-office account from being probed through it.
        if (user.FieldTeamId is not long fieldTeamId || !await userManager.IsInRoleAsync(user, FsmsRoles.FieldTeam))
        {
            return Result<AuthTokenDto>.Fail(
                InvalidTeamCredentials,
                ApiErrorCodes.AuthenticationFailed,
                httpStatusCode: 401);
        }

        if (!await FieldTeamIsActiveAsync(fieldTeamId, cancellationToken))
        {
            return Result<AuthTokenDto>.Fail(
                TeamNoLongerActive,
                ApiErrorCodes.AuthenticationFailed,
                httpStatusCode: 401);
        }

        var scopeGate = await EnsureRequiredScopeAsync(user, cancellationToken);
        if (scopeGate is not null)
        {
            return scopeGate;
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    /// <summary>
    /// Decides who owns the crew's password. With <c>ActiveDirectory:Enabled</c> on, the corporate
    /// directory is the sole authority and the stored Identity hash is never consulted — an account
    /// AD turns away cannot get in on a local password it happens to still have. With the flag off,
    /// this is exactly the Identity check the endpoint has always done.
    ///
    /// Returns null when the password is good, otherwise the failure to hand back.
    /// </summary>
    private async Task<Result<AuthTokenDto>?> VerifyFieldTeamPasswordAsync(
        ApplicationUser user,
        string userCode,
        string password,
        CancellationToken cancellationToken)
    {
        if (!activeDirectory.IsEnabled)
        {
            return await userManager.CheckPasswordAsync(user, password)
                ? null
                : Result<AuthTokenDto>.Fail(
                    InvalidTeamCredentials,
                    ApiErrorCodes.AuthenticationFailed,
                    httpStatusCode: 401);
        }

        var directoryResult = await activeDirectory.ValidateCredentialsAsync(userCode, password, cancellationToken);

        return directoryResult.Outcome switch
        {
            ActiveDirectoryAuthOutcome.Valid => null,

            // A directory that cannot answer is a 503, not a rejected password. Falling back to the
            // local hash here would quietly undo the point of turning AD on.
            ActiveDirectoryAuthOutcome.Unavailable => Result<AuthTokenDto>.Fail(
                DirectoryUnavailable,
                ApiErrorCodes.ServiceUnavailable,
                httpStatusCode: 503),

            _ => Result<AuthTokenDto>.Fail(
                InvalidTeamCredentials,
                ApiErrorCodes.AuthenticationFailed,
                httpStatusCode: 401),
        };
    }

    public async Task<Result<AuthTokenDto>> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var settings = jwtOptions.Value;

        if (string.IsNullOrWhiteSpace(settings.SigningKey))
        {
            return Result<AuthTokenDto>.Fail(
                "JWT signing is not configured.",
                ApiErrorCodes.InternalServiceError,
                httpStatusCode: 500);
        }

        var tokenHash = HashToken(refreshToken);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var storedToken = await context.UserRefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || !storedToken.IsActive(utcNow))
        {
            return Result<AuthTokenDto>.Fail(
                "Refresh token is invalid or expired.",
                ApiErrorCodes.InvalidRefreshToken,
                httpStatusCode: 401);
        }

        var user = await userManager.FindByIdAsync(storedToken.UserId);

        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            return Result<AuthTokenDto>.Fail(
                "Refresh token is invalid or expired.",
                ApiErrorCodes.InvalidRefreshToken,
                httpStatusCode: 401);
        }

        if (user.FieldTeamId is long fieldTeamId &&
            !await FieldTeamIsActiveAsync(fieldTeamId, cancellationToken))
        {
            return Result<AuthTokenDto>.Fail(
                TeamNoLongerActive,
                ApiErrorCodes.InvalidRefreshToken,
                httpStatusCode: 401);
        }

        // Same gate as login: no territory means the session must not be renewed.
        var scopeGate = await EnsureRequiredScopeAsync(user, cancellationToken);
        if (scopeGate is not null)
        {
            return scopeGate;
        }

        storedToken.Revoke(utcNow);
        await context.SaveChangesAsync(cancellationToken);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<Result<bool>> LogoutAsync(
        string userId,
        string? refreshToken,
        CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var sessions = context.UserRefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > utcNow);

        // Named token: sign this one device out. No token: sign every session of the account out,
        // which is what a mobile client wants when the crew hands the device back.
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var tokenHash = HashToken(refreshToken);
            sessions = sessions.Where(x => x.TokenHash == tokenHash);
        }

        var active = await sessions.ToListAsync(cancellationToken);

        foreach (var session in active)
        {
            session.Revoke(utcNow);
        }

        if (active.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        // Success either way — see IAuthService.LogoutAsync on why nothing is said about what was found.
        return Result<bool>.Success(true);
    }

    public async Task<Result<SsoSignInResultDto>> SignInWithSamlAsync(
        string nameId,
        string? sessionIndex,
        CancellationToken cancellationToken)
    {
        var trimmedNameId = nameId.Trim();

        // Identity's NormalizedUserName makes this case-insensitive, so the assertion's NameID only
        // has to match the account's user name, not its casing. SSO never creates an account:
        // an admin provisions it first, which is also what assigns roles and territory.
        var user = await userManager.FindByNameAsync(trimmedNameId);

        if (user is null)
        {
            return Denied(SsoDefaults.DenialReasons.NotProvisioned);
        }

        // The mirror of the guard on the mobile door: a crew account signs in on the mobile app with
        // its team code, never through the back-office SSO route.
        if (user.FieldTeamId is not null || await userManager.IsInRoleAsync(user, FsmsRoles.FieldTeam))
        {
            return Denied(SsoDefaults.DenialReasons.CrewAccount);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Denied(SsoDefaults.DenialReasons.Inactive);
        }

        // Territory is required for SSO exactly as it is for password login, so federating cannot be
        // used to walk around a gate the other door enforces.
        if (!await userManager.IsInRoleAsync(user, Roles.Administrator) &&
            !await HasActiveScopeAsync(OrgScopeOwnerTypes.User, user.Id, cancellationToken))
        {
            return Denied(SsoDefaults.DenialReasons.NoScope);
        }

        var settings = ssoOptions.Value;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var codePlainText = GenerateRefreshToken();

        // Codes live for about a minute, so every earlier one this account holds is already dead.
        // Clearing them here keeps a table on the sign-in path from growing without bound, without
        // needing a scheduled job to do it.
        await context.SsoAuthorizationCodes
            .Where(x => x.UserId == user.Id && (x.ConsumedAtUtc != null || x.ExpiresAtUtc <= utcNow))
            .ExecuteDeleteAsync(cancellationToken);

        context.SsoAuthorizationCodes.Add(SsoAuthorizationCode.Create(
            user.Id,
            HashToken(codePlainText),
            sessionIndex,
            utcNow.AddSeconds(settings.AuthorizationCodeLifetimeSeconds),
            utcNow));

        await context.SaveChangesAsync(cancellationToken);

        return Result<SsoSignInResultDto>.Success(SsoSignInResultDto.Granted(codePlainText));

        static Result<SsoSignInResultDto> Denied(string reason) =>
            Result<SsoSignInResultDto>.Success(SsoSignInResultDto.Denied(reason));
    }

    public async Task<Result<AuthTokenDto>> ExchangeSsoCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var codeHash = HashToken(code);

        var stored = await context.SsoAuthorizationCodes
            .FirstOrDefaultAsync(x => x.CodeHash == codeHash, cancellationToken);

        // Missing, expired and already-spent all answer alike, so the endpoint cannot be used to
        // probe for live codes.
        if (stored is null || !stored.IsActive(utcNow))
        {
            return Result<AuthTokenDto>.Fail(
                "Sign-in code is invalid or expired.",
                ApiErrorCodes.AuthenticationFailed,
                httpStatusCode: 401);
        }

        stored.Consume(utcNow);
        await context.SaveChangesAsync(cancellationToken);

        var user = await userManager.FindByIdAsync(stored.UserId);

        // Re-checked rather than trusted from a moment ago: the account can be locked or stripped of
        // territory between the assertion landing and the code being spent.
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            return Result<AuthTokenDto>.Fail(
                "Sign-in code is invalid or expired.",
                ApiErrorCodes.AuthenticationFailed,
                httpStatusCode: 401);
        }

        var scopeGate = await EnsureRequiredScopeAsync(user, cancellationToken);
        if (scopeGate is not null)
        {
            return scopeGate;
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    private async Task<Result<AuthTokenDto>> IssueTokensAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var settings = jwtOptions.Value;

        if (string.IsNullOrWhiteSpace(settings.SigningKey))
        {
            return Result<AuthTokenDto>.Fail(
                "JWT signing is not configured.",
                ApiErrorCodes.InternalServiceError,
                httpStatusCode: 500);
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var roles = await userManager.GetRolesAsync(user);
        var permissions = await permissionResolver.GetPermissionCodesForUserAsync(user.Id, cancellationToken);
        var accessToken = JwtTokenGenerator.GenerateAccessToken(
            user,
            roles.ToList(),
            permissions,
            settings,
            utcNow);
        var refreshTokenPlainText = GenerateRefreshToken();
        var refreshTokenHash = HashToken(refreshTokenPlainText);
        var refreshExpiresAt = utcNow.AddDays(settings.RefreshTokenExpiryDays);

        context.UserRefreshTokens.Add(
            UserRefreshToken.Create(user.Id, refreshTokenHash, refreshExpiresAt, utcNow));

        await context.SaveChangesAsync(cancellationToken);

        var scope = await LoadScopeAsync(user, roles, cancellationToken);

        return Result<AuthTokenDto>.Success(new AuthTokenDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenPlainText,
            ExpiresInSeconds = settings.ExpiryMinutes * 60,
            UserName = user.UserName ?? string.Empty,
            Roles = roles.ToList(),
            Permissions = permissions,
            FieldTeamId = user.FieldTeamId,
            FieldTeamName = scope.FieldTeamName,
            ScopeOwnerType = scope.OwnerType,
            IsUnrestrictedScope = scope.IsUnrestricted,
            Scopes = scope.Scopes,
        });
    }

    /// <summary>
    /// The territory the account signing in works under, so a mobile client has its scope in hand
    /// from the token response rather than after a follow-up profile call. Mirrors what
    /// <c>OrgScopeService.GetCurrentUserScopeAsync</c> resolves for an already-authenticated caller:
    /// a field-team login reads the rows on its Team, everyone else the rows on their own user.
    /// </summary>
    private async Task<AuthScopeContext> LoadScopeAsync(
        ApplicationUser user,
        IList<string> roles,
        CancellationToken cancellationToken)
    {
        string ownerType;
        string ownerId;
        string? fieldTeamName = null;

        if (user.FieldTeamId is long fieldTeamId)
        {
            ownerType = OrgScopeOwnerTypes.Team;
            ownerId = fieldTeamId.ToString(CultureInfo.InvariantCulture);
            fieldTeamName = await context.FieldTeams
                .AsNoTracking()
                .Where(t => t.Id == fieldTeamId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            ownerType = OrgScopeOwnerTypes.User;
            ownerId = user.Id;
        }

        var scopeRows = await context.OrgScopes
            .AsNoTracking()
            .Where(x => x.OwnerType == ownerType && x.OwnerId == ownerId && x.IsActive)
            .Select(x => new { x.Id, x.Level, x.Code, x.DepartmentId })
            .ToListAsync(cancellationToken);

        // Holding no rows already means "everywhere" throughout the scope engine, so it reads the
        // same here as the two roles that bypass territory outright.
        var isUnrestricted =
            roles.Contains(Roles.Administrator, StringComparer.Ordinal) ||
            roles.Contains(FsmsRoles.Monitor, StringComparer.Ordinal) ||
            scopeRows.Count == 0;

        if (scopeRows.Count == 0)
        {
            return new AuthScopeContext(ownerType, fieldTeamName, isUnrestricted, []);
        }

        var departmentNames = await LookupNameCache.GetDepartmentNamesAsync(
            context, cache, cacheSettings.Value, cancellationToken);
        var cbuNames = await LookupNameCache.GetCbuNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var branchNames = await LookupNameCache.GetBranchNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var operationAreaNames = await LookupNameCache.GetOperationAreaNamesAsync(
            context, cache, cacheSettings.Value, cancellationToken);

        var scopes = scopeRows
            .Select(x =>
            {
                var department = departmentNames.FindDepartment(x.DepartmentId);

                // A row sits at exactly one level, so only the matching map is ever consulted.
                var codeName = x.Level switch
                {
                    OrgScopeLevels.Cbu => cbuNames.FindCbu(x.Code),
                    OrgScopeLevels.Branch => branchNames.FindBranch(x.Code),
                    OrgScopeLevels.OperationArea => operationAreaNames.FindOperationArea(x.Code),
                    _ => null,
                };

                return new AuthScopeDto
                {
                    ScopeId = x.Id,
                    Level = x.Level,
                    Code = x.Code,
                    CodeNameEn = codeName?.NameEn,
                    CodeNameAr = codeName?.NameAr,
                    DepartmentId = x.DepartmentId,
                    DepartmentNameEn = department?.NameEn,
                    DepartmentNameAr = department?.NameAr,
                };
            })
            .ToList();

        return new AuthScopeContext(ownerType, fieldTeamName, isUnrestricted, scopes);
    }

    /// <summary>
    /// Blocks sign-in when the account has no active territory. Super Admin
    /// (<see cref="Roles.Administrator"/>) is exempt; field-team logins need a Team scope row;
    /// everyone else needs a User scope row.
    /// </summary>
    private async Task<Result<AuthTokenDto>?> EnsureRequiredScopeAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        if (await userManager.IsInRoleAsync(user, Roles.Administrator))
        {
            return null;
        }

        if (user.FieldTeamId is long fieldTeamId)
        {
            return await HasActiveScopeAsync(
                    OrgScopeOwnerTypes.Team,
                    fieldTeamId.ToString(CultureInfo.InvariantCulture),
                    cancellationToken)
                ? null
                : Result<AuthTokenDto>.Fail(
                    TeamScopeRequired,
                    ApiErrorCodes.AuthenticationFailed,
                    httpStatusCode: 401);
        }

        return await HasActiveScopeAsync(OrgScopeOwnerTypes.User, user.Id, cancellationToken)
            ? null
            : Result<AuthTokenDto>.Fail(
                UserScopeRequired,
                ApiErrorCodes.AuthenticationFailed,
                httpStatusCode: 401);
    }

    private Task<bool> FieldTeamIsActiveAsync(long fieldTeamId, CancellationToken cancellationToken) =>
        context.FieldTeams
            .AsNoTracking()
            .AnyAsync(t => t.Id == fieldTeamId && t.IsActive, cancellationToken);

    private Task<bool> HasActiveScopeAsync(
        string ownerType,
        string ownerId,
        CancellationToken cancellationToken) =>
        context.OrgScopes
            .AsNoTracking()
            .AnyAsync(
                x => x.OwnerType == ownerType && x.OwnerId == ownerId && x.IsActive,
                cancellationToken);

    private static string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string HashToken(string token)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>The resolved territory of the account being issued a token.</summary>
    private sealed record AuthScopeContext(
        string OwnerType,
        string? FieldTeamName,
        bool IsUnrestricted,
        IReadOnlyList<AuthScopeDto> Scopes);
}
