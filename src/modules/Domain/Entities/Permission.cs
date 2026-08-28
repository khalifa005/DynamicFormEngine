using Shared.Core.Entities;

namespace KH.Domain.Entities;

public sealed class Permission : LookupEntity
{
    private Permission()
    {
    }

    public Permission(string code, string module, string nameEn, string nameAr, string? description = null)
    {
        Code = code;
        Module = module;
        NameEn = nameEn;
        NameAr = nameAr;
        Description = description ?? string.Empty;
        IsActive = true;
    }

    public string Module { get; private set; } = default!;

    public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();
}
