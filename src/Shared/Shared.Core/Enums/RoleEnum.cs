namespace Shared.Core.Enums;

public sealed class RoleEnum(int id, string nameAr, string nameEn) : DynamicEnumItem<int>(id, nameAr, nameEn)
{
    public static readonly RoleEnum Admin = new(1, "مدير النظام", RolesConsts.Admin);
    public static readonly RoleEnum Employee = new(2, "موظف", RolesConsts.Employee);

    private static readonly List<RoleEnum> _allRoles = [Admin, Employee];
    public static IReadOnlyList<RoleEnum> GetAll() => _allRoles.AsReadOnly();
    public static RoleEnum GetByName(string name) => _allRoles.First(x => x.NameEn.Equals(name, StringComparison.OrdinalIgnoreCase));
}

public static class RolesConsts
{
    public const string Admin = "admin";
    public const string Employee = "employee";
}