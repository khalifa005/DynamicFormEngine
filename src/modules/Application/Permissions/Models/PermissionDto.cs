namespace KH.Application.Permissions.Models;

public sealed class PermissionDto
{
    public int Id { get; init; }

    public string Code { get; init; } = default!;

    public string Module { get; init; } = default!;

    public string NameEn { get; init; } = default!;

    public string NameAr { get; init; } = default!;

    public bool IsActive { get; init; }
}
