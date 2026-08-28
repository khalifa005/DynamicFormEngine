namespace Shared.Core.Interfaces;

public interface ICurrentUser
{
    string? UserId { get; }
    string? Email { get; }
    string? Username { get; }
    string[] Roles { get; }
    string ClientIp { get; }
    string SamlNameId { get; }
    bool IsAuthenticated { get; }
}
