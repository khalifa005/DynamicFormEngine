namespace Shared.Core.Enums;

public static class DynamicEnumExtensions
{
    /// <summary>
    /// Value persisted in database columns (Code preferred, otherwise English name).
    /// </summary>
    public static string GetStorageCode<T>(this DynamicEnumItem<T> item) where T : IComparable =>
        item.Code ?? item.NameEn;
}
