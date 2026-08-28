namespace Shared.Core.Enums;

public abstract class DynamicEnumItem<T>(
    T id,
    string nameAr,
    string nameEn,
    string? code = null
    ) where T : IComparable
{
    public T Id { get; private set; } = id;
    public string NameAr { get; private set; } = nameAr;
    public string NameEn { get; private set; } = nameEn;
    public string? Code { get; private set; } = code;

    public string GetName(string locale = "EN") =>
        locale.Equals("AR", StringComparison.CurrentCultureIgnoreCase) ? NameAr : NameEn;
}
