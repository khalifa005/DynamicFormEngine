using KellermanSoftware.CompareNetObjects;
using System.Collections;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Shared.Core.Helpers;

public static class ObjectExtensions
{
    public static string ToJSON(this object obj)
        => JsonSerializer.Serialize(obj, JsonSerializationOptions());
    public static T? ToModel<T>(this string strJSON)
    {
        return strJSON.IsEmpty() ? default : JsonSerializer.Deserialize<T>(strJSON, JsonSerializationOptions());
    }


    public static bool ObjectChanged<T>(this T model1, T model2, params string[] ignoreMembers) where T : class
    {
        CompareLogic compareLogic = new();
        compareLogic.Config.TreatStringEmptyAndNullTheSame = true;
        if (ignoreMembers.Length > 0) compareLogic.Config.MembersToIgnore.AddRange(ignoreMembers);
        var result = compareLogic.Compare(model1, model2);
        return !result.AreEqual;
    }
    public static bool IsEmpty<T>(this T value) =>
        value == null || value.Equals(default(T)) || (value is IList list && list.Count == 0) || (value is string str && string.IsNullOrWhiteSpace(str)) || (value is Guid guid && guid == Guid.Empty);

    public static bool CheckAllBoolProps(this object obj) =>
        obj.GetType().GetProperties().Where(prop => prop.PropertyType == typeof(bool))
       .Any(prop => (bool?)prop.GetValue(obj, null) == true);

    public static JsonSerializerOptions JsonSerializationOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Arabic),
        };
        options.Converters.Add(new DateOnlyConverter());
        return options;
    }
}

public sealed class DateOnlyConverter : JsonConverter<DateOnly>
{
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateOnly.FromDateTime(reader.GetDateTime());
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        var isoDate = value.ToString("O");
        writer.WriteStringValue(isoDate);
    }
}