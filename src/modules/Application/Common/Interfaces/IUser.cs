namespace KH.Application.Common.Interfaces;

public interface IUser
{
    string? Id { get; }

    string? UserName { get; }

    string? Email { get; }

    IReadOnlyList<string> Roles { get; }

    IReadOnlyList<string> Permissions { get; }

    /// <summary>The <c>TEAMS</c> row id behind a field-team login, if this account is one.</summary>
    long? FieldTeamId { get; }

    bool IsInRole(string role);

    bool HasPermission(string permissionCode);
}
