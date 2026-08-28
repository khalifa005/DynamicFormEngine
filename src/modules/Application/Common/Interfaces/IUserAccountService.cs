using Shared.Core.Common;

namespace KH.Application.Common.Interfaces;

/// <summary>
/// Back-office account administration, kept behind an interface so ASP.NET Identity's
/// <c>UserManager</c> never leaks into the Application layer.
///
/// Deliberately separate from <see cref="IIdentityService"/>: that one answers questions about the
/// caller ("is this user in this role?") and speaks the older <c>Common.Models.Result</c>, while
/// this one administers other people's accounts and returns the <see cref="Result{T}"/> every FSMS
/// slice already returns. Folding the two together would mean one interface with two result types.
/// </summary>
public interface IUserAccountService
{
    Task<Result<PagedResult<UserAccount>>> GetUsersAsync(UserAccountQuery query, CancellationToken cancellationToken);

    Task<Result<UserAccount>> GetUserAsync(string userId, CancellationToken cancellationToken);

    Task<Result<string>> CreateUserAsync(NewUserAccount account, CancellationToken cancellationToken);

    Task<Result<string>> UpdateUserAsync(string userId, EditedUserAccount account, CancellationToken cancellationToken);

    /// <summary>
    /// Enables or disables a login. Implemented with a permanent lockout rather than a delete: the
    /// account still owns surveys it created and audit rows that name it, and deleting it would
    /// leave those pointing at nothing.
    /// </summary>
    Task<Result<string>> SetUserEnabledAsync(string userId, bool isEnabled, CancellationToken cancellationToken);

    Task<Result<string>> ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken);

    /// <summary>
    /// Enables or disables the login belonging to a field crew. Retiring a team has to reach its
    /// login too, or the crew keeps signing in to an app that no longer has work for them. A team
    /// with no login (one seeded before logins existed) is a no-op rather than an error.
    /// </summary>
    Task<Result<bool>> SetTeamLoginEnabledAsync(long fieldTeamId, bool isEnabled, CancellationToken cancellationToken);

    /// <summary>The roles an account may be given, for the admin screen's picker.</summary>
    Task<Result<IReadOnlyList<string>>> GetAssignableRolesAsync(CancellationToken cancellationToken);
}

/// <summary>Filters for the user admin grid. Paging is 1-based, matching every other FSMS query.</summary>
public sealed record UserAccountQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;

    /// <summary>Matches user name or email.</summary>
    public string? SearchTerm { get; init; }

    public string? Role { get; init; }
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// Leaves out logins that belong to a field crew. Those are created and retired from the teams
    /// screen alongside the team itself, so listing them here would offer a second, partial way to
    /// manage the same thing. Filtered in the query rather than after paging, so the count agrees
    /// with the page.
    /// </summary>
    public bool ExcludeFieldTeamAccounts { get; init; }
}

/// <summary>An account as the admin screens see it. Scopes are attached by the query handler.</summary>
public sealed record UserAccount
{
    public string Id { get; init; } = default!;
    public string UserName { get; init; } = default!;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }

    /// <summary>Set when the login belongs to a field crew rather than the back office.</summary>
    public long? FieldTeamId { get; init; }

    public bool IsEnabled { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}

public sealed record NewUserAccount
{
    public string UserName { get; init; } = default!;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string Password { get; init; } = default!;
    public long? FieldTeamId { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}

public sealed record EditedUserAccount
{
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];

    // No FieldTeamId: the crew link is set once, when the teams screen raises the login alongside
    // its team, and never edited. Carrying it here would mean every ordinary edit had to remember
    // to resend it or silently detach the crew from its team.
}
