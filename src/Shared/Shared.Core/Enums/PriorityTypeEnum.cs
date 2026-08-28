namespace Shared.Core.Enums;

public sealed class PriorityTypeEnum(int id, string nameAr, string nameEn, string code)
    : DynamicEnumItem<int>(id, nameAr, nameEn, code)
{
    public static readonly PriorityTypeEnum High = new(1, "عالي", "High", "H");
    public static readonly PriorityTypeEnum Mid = new(2, "متوسط", "Mid", "M");
    public static readonly PriorityTypeEnum Low = new(3, "ضعيفة", "Low", "L");
    public static PriorityTypeEnum Default => Low;

    private static readonly List<PriorityTypeEnum> _allRoles = [High, Mid, Low];

    public static IReadOnlyList<PriorityTypeEnum> GetAll() => _allRoles.AsReadOnly();

    public static PriorityTypeEnum? GetById(int id) =>
        _allRoles.Find(r => r.Id.CompareTo(id) == 0);

    /// <summary>Kedana/external code: H, M, or L (case-insensitive).</summary>
    public static PriorityTypeEnum? GetByCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var trimmed = code.Trim();
        return _allRoles.Find(r =>
            string.Equals(r.Code, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}