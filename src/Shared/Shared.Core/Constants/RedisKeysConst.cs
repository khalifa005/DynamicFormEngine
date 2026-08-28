using System.Reflection;

namespace Shared.Core.Constants;

public class RedisKeysConst
{
    public const string BaseKey = "MK:Lookups:";
    public const string AreasKey = "Apps";
    public const string CategoryKey = "Categories";
    public const string SubCategoryKey = "SubCategories";
    public const string PriorityKey = "Priorities";



    public static List<string?> GetAll()
    {
        List<string?> queueNames = [.. typeof(RedisKeysConst)
           .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
           .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string))
           .Where(fi => fi.GetRawConstantValue() != null)
           .Select(fi => fi.GetRawConstantValue() as string)];

        return queueNames;
    }
}