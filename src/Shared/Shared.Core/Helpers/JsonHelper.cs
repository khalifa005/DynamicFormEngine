using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Shared.Core.Helpers;

public static class JsonHelper
{
    public static JsonSerializerOptions JsonOption { get; set; } = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping //for arabic
                                                              //WriteIndented = true
    };
    public static JsonSerializerOptions JsonOptionLogNull { get; set; } = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping //for arabic
    };
    public static string SerializeWithNull(object request)
    {
        if (request == null) return "";
        return JsonSerializer.Serialize(request, JsonOptionLogNull);
    }
    public static StringContent GetJsonStringContentReq(object request)
    {
        string serialized = JsonSerializer.Serialize(request);
        return new(serialized, Encoding.UTF8, "application/json");
    }

    public static string Serialize(object request)
        => JsonSerializer.Serialize(request, JsonOption);

    public static T? Deserialize<T>(string response)
        => JsonSerializer.Deserialize<T>(response, JsonOption);
}
