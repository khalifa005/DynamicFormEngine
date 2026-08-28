using KH.Application.Common.Models;

namespace KH.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<string?> GetUserNameAsync(string userId);

    /// <summary>
    /// Display names for the given account ids, keyed by id, in one query. What a document naming
    /// "who did this" needs: the ids are stamped on the rows, and resolving them one at a time is a
    /// round trip per stamp. An id with no account is left out rather than mapped to a blank, so the
    /// caller can fall back to showing the id itself.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetUserNamesAsync(
        IEnumerable<string?> userIds,
        CancellationToken cancellationToken);

    Task<bool> IsInRoleAsync(string userId, string role);

    Task<bool> AuthorizeAsync(string userId, string policyName);

    Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password);

    Task<Result> DeleteUserAsync(string userId);
}
