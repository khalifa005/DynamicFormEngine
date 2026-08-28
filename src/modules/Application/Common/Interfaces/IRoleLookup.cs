namespace KH.Application.Common.Interfaces;

/// <summary>Identity role name and its primary key, as the application layer needs them.</summary>
public sealed record RoleSummary(string Id, string Name);

public interface IRoleLookup
{
    Task<string?> GetRoleIdByNameAsync(string roleName, CancellationToken cancellationToken = default);

    Task<bool> RoleExistsAsync(string roleName, CancellationToken cancellationToken = default);

    /// <summary>Every role the identity store holds.</summary>
    Task<IReadOnlyList<RoleSummary>> GetRolesAsync(CancellationToken cancellationToken = default);
}
