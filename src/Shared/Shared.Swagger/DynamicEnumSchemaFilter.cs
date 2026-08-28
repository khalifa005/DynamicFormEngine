using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Shared.Core.Enums;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Shared.Swagger;

public class DynamicEnumSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        var type = context.Type;

        if (!IsDynamicEnum(type))
        {
            return;
        }

        if (schema is OpenApiSchema concreteSchema)
        {
            concreteSchema.Type = JsonSchemaType.String;
        }

        IEnumerable? items = null;

        // Try calling static GetAll() method
        var getAllMethod = type.GetMethod("GetAll", BindingFlags.Public | BindingFlags.Static);
        if (getAllMethod != null)
        {
            items = getAllMethod.Invoke(null, null) as IEnumerable;
        }

        // Fallback: reflect static fields of the type
        if (items == null)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => type.IsAssignableFrom(f.FieldType))
                .Select(f => f.GetValue(null))
                .Where(v => v != null);
            items = fields;
        }

        if (items == null)
        {
            return;
        }

        schema.Enum?.Clear();
        schema.Properties?.Clear();

        foreach (var item in items)
        {
            if (item == null) continue;

            var codeProp = item.GetType().GetProperty("Code")?.GetValue(item)?.ToString();
            var nameEnProp = item.GetType().GetProperty("NameEn")?.GetValue(item)?.ToString();

            var stringValue = !string.IsNullOrWhiteSpace(codeProp) ? codeProp : nameEnProp ?? item.ToString();

            if (!string.IsNullOrWhiteSpace(stringValue))
            {
                var jsonVal = JsonValue.Create(stringValue);
                if (schema.Enum != null && jsonVal != null)
                {
                    schema.Enum.Add(jsonVal);
                }
            }
        }
    }

    private static bool IsDynamicEnum(Type type)
    {
        var current = type;
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
