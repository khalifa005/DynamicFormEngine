namespace KH.Application.Common.Interfaces;

public interface IPermissionResolver
{
    Task<IReadOnlyList<string>> GetPermissionCodesForUserAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetPermissionCodesForRoleAsync(
        string roleId,
        CancellationToken cancellationToken = default);

    void InvalidateUser(string userId);

    Task InvalidateRoleAsync(string roleId, CancellationToken cancellationToken = default);
}
