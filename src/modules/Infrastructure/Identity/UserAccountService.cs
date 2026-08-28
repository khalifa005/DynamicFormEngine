using KH.Application.Common.Interfaces;
using KH.Domain.Constants;
using KH.Domain.Constants.Fsms;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Core.Common;

namespace KH.Infrastructure.Identity;

/// <summary>
/// Account administration over ASP.NET Identity. Every failure comes back as a
/// <see cref="Result{T}"/> carrying Identity's own messages, so a rejected password tells the
/// operator which rule it broke rather than "create failed".
/// </summary>
public sealed class UserAccountService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager)
    : IUserAccountService
{
    /// <summary>
    /// Lockout end used to disable an account. <see cref="DateTimeOffset.MaxValue"/> is Identity's
    /// own convention for "until further notice" and is what <c>SetLockoutEndDateAsync</c> expects.
    /// </summary>
    private static readonly DateTimeOffset PermanentLockout = DateTimeOffset.MaxValue;

    public async Task<Result<PagedResult<UserAccount>>> GetUsersAsync(
        UserAccountQuery query,
        CancellationToken cancellationToken)
    {
        var users = userManager.Users.AsNoTracking();

        if (query.ExcludeFieldTeamAccounts)
        {
            users = users.Where(x => x.FieldTeamId == null);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            users = users.Where(x =>
                (x.UserName != null && x.UserName.Contains(term)) ||
                (x.Email != null && x.Email.Contains(term)));
        }

        if (query.IsEnabled is bool isEnabled)
        {
            // Disabled means a lockout that has not yet passed; anything else is an active login.
            users = isEnabled
                ? users.Where(x => x.LockoutEnd == null || x.LockoutEnd <= DateTimeOffset.UtcNow)
                : users.Where(x => x.LockoutEnd != null && x.LockoutEnd > DateTimeOffset.UtcNow);
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var role = query.Role.Trim();

            // Role membership lives in a join table Identity does not expose on IQueryable<TUser>,
            // so it is filtered through the role's own user-id set rather than in the same query.
            var roleUserIds = await RoleUserIdsAsync(role, cancellationToken);
            users = users.Where(x => roleUserIds.Contains(x.Id));
        }

        var totalCount = await users.CountAsync(cancellationToken);

        var page = await users
            .OrderBy(x => x.UserName)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = new List<UserAccount>(page.Count);

        foreach (var user in page)
        {
            items.Add(await ToAccountAsync(user));
        }

        return Result<PagedResult<UserAccount>>.Success(
            new PagedResult<UserAccount>(items, totalCount, query.PageNumber, query.PageSize));
    }

    public async Task<Result<UserAccount>> GetUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);

        return user is null
            ? NotFound<UserAccount>()
            : Result<UserAccount>.Success(await ToAccountAsync(user));
    }

    public async Task<Result<string>> CreateUserAsync(NewUserAccount account, CancellationToken cancellationToken)
    {
        var existing = await userManager.FindByNameAsync(account.UserName);

        if (existing is not null)
        {
            return Result<string>.Fail(
                $"A user named '{account.UserName}' already exists.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        var rolesResult = await ValidateRolesAsync(account.Roles, cancellationToken);

        if (!rolesResult.IsSuccess)
        {
            return Result<string>.Fail(rolesResult.Errors);
        }

        var user = new ApplicationUser
        {
            UserName = account.UserName.Trim(),
            Email = string.IsNullOrWhiteSpace(account.Email) ? null : account.Email.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(account.PhoneNumber) ? null : account.PhoneNumber.Trim(),
            FieldTeamId = account.FieldTeamId
        };

        var created = await userManager.CreateAsync(user, account.Password);

        if (!created.Succeeded)
        {
            return Failed<string>(created);
        }

        if (account.Roles.Count > 0)
        {
            var assigned = await userManager.AddToRolesAsync(user, account.Roles);

            if (!assigned.Succeeded)
            {
                // The account exists but is roleless, which is worse than not existing: it can log
                // in and see nothing. Roll it back so the operator retries against a clean slate.
                await userManager.DeleteAsync(user);
                return Failed<string>(assigned);
            }
        }

        return Result<string>.Success(user.Id);
    }

    public async Task<Result<string>> UpdateUserAsync(
        string userId,
        EditedUserAccount account,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return NotFound<string>();
        }

        var rolesResult = await ValidateRolesAsync(account.Roles, cancellationToken);

        if (!rolesResult.IsSuccess)
        {
            return Result<string>.Fail(rolesResult.Errors);
        }

        user.Email = string.IsNullOrWhiteSpace(account.Email) ? null : account.Email.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(account.PhoneNumber) ? null : account.PhoneNumber.Trim();

        var updated = await userManager.UpdateAsync(user);

        if (!updated.Succeeded)
        {
            return Failed<string>(updated);
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        var removals = currentRoles.Except(account.Roles, StringComparer.Ordinal).ToList();
        var additions = account.Roles.Except(currentRoles, StringComparer.Ordinal).ToList();

        if (removals.Count > 0)
        {
            var removed = await userManager.RemoveFromRolesAsync(user, removals);

            if (!removed.Succeeded)
            {
                return Failed<string>(removed);
            }
        }

        if (additions.Count > 0)
        {
            var added = await userManager.AddToRolesAsync(user, additions);

            if (!added.Succeeded)
            {
                return Failed<string>(added);
            }
        }

        return Result<string>.Success(user.Id);
    }

    public async Task<Result<string>> SetUserEnabledAsync(
        string userId,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return NotFound<string>();
        }

        // Lockout has to be switched on for an end date to mean anything — an account created with
        // it disabled would ignore the date entirely and stay reachable.
        var enabledLockout = await userManager.SetLockoutEnabledAsync(user, true);

        if (!enabledLockout.Succeeded)
        {
            return Failed<string>(enabledLockout);
        }

        var result = await userManager.SetLockoutEndDateAsync(user, isEnabled ? null : PermanentLockout);

        return result.Succeeded ? Result<string>.Success(user.Id) : Failed<string>(result);
    }

    public async Task<Result<string>> ResetPasswordAsync(
        string userId,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return NotFound<string>();
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);

        return result.Succeeded ? Result<string>.Success(user.Id) : Failed<string>(result);
    }

    public async Task<Result<bool>> SetTeamLoginEnabledAsync(
        long fieldTeamId,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        var logins = await userManager.Users
            .Where(x => x.FieldTeamId == fieldTeamId)
            .ToListAsync(cancellationToken);

        foreach (var login in logins)
        {
            var enabledLockout = await userManager.SetLockoutEnabledAsync(login, true);

            if (!enabledLockout.Succeeded)
            {
                return Failed<bool>(enabledLockout);
            }

            var result = await userManager.SetLockoutEndDateAsync(login, isEnabled ? null : PermanentLockout);

            if (!result.Succeeded)
            {
                return Failed<bool>(result);
            }
        }

        return Result<bool>.Success(true);
    }

    public async Task<Result<IReadOnlyList<string>>> GetAssignableRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await roleManager.Roles
            .AsNoTracking()
            .Where(x => x.Name != null)
            .Select(x => x.Name!)
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<string>>.Success(roles);
    }

    /// <summary>
    /// Rejects a role name that does not exist. Identity's <c>AddToRolesAsync</c> throws on an
    /// unknown role rather than returning a failed result, so it is checked before we get there.
    /// </summary>
    private async Task<Result<bool>> ValidateRolesAsync(
        IReadOnlyList<string> roles,
        CancellationToken cancellationToken)
    {
        if (roles.Count == 0)
        {
            return Result<bool>.Success(true);
        }

        var known = await roleManager.Roles
            .AsNoTracking()
            .Where(x => x.Name != null)
            .Select(x => x.Name!)
            .ToListAsync(cancellationToken);

        var unknown = roles.Except(known, StringComparer.Ordinal).ToList();

        return unknown.Count == 0
            ? Result<bool>.Success(true)
            : Result<bool>.Fail(
                $"Unknown role(s): {string.Join(", ", unknown)}.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
    }

    private async Task<HashSet<string>> RoleUserIdsAsync(string role, CancellationToken cancellationToken)
    {
        var users = await userManager.GetUsersInRoleAsync(role);

        return users.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
    }

    private async Task<UserAccount> ToAccountAsync(ApplicationUser user) => new()
    {
        Id = user.Id,
        UserName = user.UserName ?? string.Empty,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber,
        FieldTeamId = user.FieldTeamId,
        IsEnabled = user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow,
        Roles = [.. await userManager.GetRolesAsync(user)]
    };

    private static Result<T> NotFound<T>() =>
        Result<T>.Fail("User not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);

    private static Result<T> Failed<T>(IdentityResult result) =>
        Result<T>.Fail(
            string.Join(" ", result.Errors.Select(e => e.Description)),
            ApiErrorCodes.ValidationError,
            httpStatusCode: 400);
}
