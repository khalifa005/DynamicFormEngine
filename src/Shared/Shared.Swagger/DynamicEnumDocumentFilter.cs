using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Shared.Core.Enums;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Shared.Swagger;

public class DynamicEnumDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var dynamicEnumTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Array.Empty<Type>(); }
            })
            .Where(IsDynamicEnum);

        foreach (var type in dynamicEnumTypes)
        {
            if (swaggerDoc.Components == null || swaggerDoc.Components.Schemas == null)
            {
                continue;
            }

            IEnumerable? items = null;
            var getAllMethod = type.GetMethod("GetAll", BindingFlags.Public | BindingFlags.Static);
            if (getAllMethod != null)
            {
                items = getAllMethod.Invoke(null, null) as IEnumerable;
            }

            if (items == null)
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(f => type.IsAssignableFrom(f.FieldType))
                    .Select(f => f.GetValue(null))
                    .Where(v => v != null);
                items = fields;
            }

            var enumValues = new List<JsonNode>();
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null) continue;
                    var codeProp = item.GetType().GetProperty("Code")?.GetValue(item)?.ToString();
                    var nameEnProp = item.GetType().GetProperty("NameEn")?.GetValue(item)?.ToString();
                    var stringValue = !string.IsNullOrWhiteSpace(codeProp) ? codeProp : nameEnProp ?? item.ToString();

                    if (!string.IsNullOrWhiteSpace(stringValue))
                    {
                        var jsonVal = JsonValue.Create(stringValue);
                        if (jsonVal != null && !enumValues.Any(e => e.ToString() == stringValue))
                        {
                            enumValues.Add(jsonVal);
                        }
                    }
                }
            }

            var enumSchema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = enumValues
            };

            swaggerDoc.Components.Schemas[type.Name] = enumSchema;
        }
    }

    private static bool IsDynamicEnum(Type type)
    {
        if (type.IsAbstract || type.IsInterface) return false;

        var current = type.BaseType;
        while (current != null && current != typeof(object))
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(DynamicEnumItem<>))
            {
                return true;
            }
            current = current.BaseType;
        }
        return false;
    }
}
