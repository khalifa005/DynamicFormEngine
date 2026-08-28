namespace KH.Application.Fsms.Users.Models;

/// <summary>Row shape for the user admin grid.</summary>
public sealed class UserListItemDto
{
    public string Id { get; init; } = default!;
    public string UserName { get; init; } = default!;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public long? FieldTeamId { get; init; }
    public bool IsEnabled { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}
