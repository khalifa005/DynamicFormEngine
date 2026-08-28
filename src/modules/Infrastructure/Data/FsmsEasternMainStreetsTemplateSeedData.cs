using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Templates;
using Microsoft.EntityFrameworkCore;

namespace KH.Infrastructure.Data;

/// <summary>
/// Seeds <c>SRV-EASTERN-MAIN-STREETS-001</c> from the Microsoft Forms designer
/// مبادرة المسح الميداني للمباني الكبيرة في الشوارع الرئيسية بالقطاع الشرقي.
/// Field list lives in <c>FsmsEasternMainStreetsFields.json</c>.
/// Distinct from the Excel-derived <c>SRV-MSFORMS-SURVEY-2026</c> (incomplete choice lists).
/// </summary>
internal static class FsmsEasternMainStreetsTemplateSeedData
{
    internal const string TemplateCode = "SRV-EASTERN-MAIN-STREETS-001";

    private const string NameEn = "Field Survey Initiative — Large Buildings on Main Streets (Eastern Sector)";
    private const string NameAr = "مبادرة المسح الميداني للمباني الكبيرة في الشوارع الرئيسية بالقطاع الشرقي";
    private const string FieldsResourceName = "KH.Infrastructure.Data.FsmsEasternMainStreetsFields.json";

    private static readonly string[] PhotoExtensions = ["jpg", "jpeg", "png", "heic", "webp"];

    private static readonly JsonSerializerOptions FieldJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions DefinitionJsonOptions = new()
    {
        WriteIndented = true,
    };

    internal static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.SurveyTemplates.AnyAsync(x => x.TemplateCode == TemplateCode, cancellationToken))
        {
            return;
        }

        var definitionJson = BuildDefinitionJson();

        var template = SurveyTemplate.Create(
            TemplateCode,
            NameEn,
            NameAr,
            SurveyCategories.AssetInspection,
            teamFillSlaHours: 72,
            completionSlaHours: 48,
            departmentId: 10,
            branchScope: "1110,RCBU");

        template.SetDefinition(definitionJson, NameEn, NameAr);

        context.SurveyTemplates.Add(template);
        await context.SaveChangesAsync(cancellationToken);

        var snapshots = new[]
        {
            new TemplateVersionSnapshot(TargetClients.Formly, template.DefinitionJson, BuildSnapshotJson(template)),
        };

        template.Publish("seed", snapshots, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(cancellationToken);
    }

    internal static string BuildDefinitionJson()
    {
        var fields = LoadFields();
        var elements = new JsonArray();

        foreach (var group in fields.GroupBy(f => f.Section, StringComparer.Ordinal))
        {
            var (labelEn, labelAr) = SectionLabels(group.Key);
            var section = new JsonObject
            {
                ["type"] = "section",
                ["label_en"] = labelEn,
                ["label_ar"] = labelAr,
                ["data_name"] = group.Key,
                ["description_en"] = "",
                ["description_ar"] = "",
                ["hidden"] = false,
                ["display"] = "inline",
                ["visible_conditions"] = EmptyRuleGroup(),
                ["elements"] = new JsonArray(group.Select(BuildField).ToArray()),
            };
            elements.Add(section);
        }

        var root = new JsonObject
        {
            ["name_en"] = NameEn,
            ["name_ar"] = NameAr,
            ["elements"] = elements,
        };

        return root.ToJsonString(DefinitionJsonOptions);
    }

    private static JsonObject BuildField(FieldSpec field)
    {
        var obj = new JsonObject
        {
            ["type"] = field.Type,
            ["label_en"] = field.LabelEn,
            ["label_ar"] = field.LabelAr,
            ["data_name"] = field.DataName,
            ["description_en"] = field.DescriptionEn ?? "",
            ["description_ar"] = field.DescriptionAr ?? "",
            ["hidden"] = false,
            ["default_value"] = null,
            ["required"] = field.Required,
            ["disabled"] = false,
        };

        switch (field.Type)
        {
            case "text":
            case "memo":
                obj["min_length"] = null;
                obj["max_length"] = null;
                obj["pattern"] = null;
                break;
            case "numeric":
                obj["format"] = field.NumericFormat ?? "integer";
                obj["min"] = null;
                obj["max"] = null;
                break;
            case "single_choice":
                obj["allow_other"] = field.AllowOther;
                obj["multiple"] = false;
                obj["parent_field"] = null;
                obj["choices"] = BuildChoices(field.Choices);
                break;
            case "photo":
                obj["max_files"] = field.MaxFiles ?? 1;
                obj["max_file_size_mb"] = 10;
                obj["allowed_extensions"] = new JsonArray(PhotoExtensions.Select(x => JsonValue.Create(x)).ToArray());
                break;
        }

        obj["visible_conditions"] = EmptyRuleGroup();
        obj["required_conditions"] = EmptyRuleGroup();
        return obj;
    }

    private static JsonArray BuildChoices(IReadOnlyList<ChoiceSpec>? choices)
    {
        var array = new JsonArray();
        if (choices is null)
        {
            return array;
        }

        foreach (var choice in choices)
        {
            array.Add(new JsonObject
            {
                ["value"] = choice.Value,
                ["label_en"] = choice.LabelEn,
                ["label_ar"] = choice.LabelAr,
                ["dependency_value"] = null,
            });
        }

        return array;
    }

    private static JsonObject EmptyRuleGroup() => new()
    {
        ["match"] = "all",
        ["conditions"] = new JsonArray(),
        ["preserve_data"] = false,
    };

    private static (string En, string Ar) SectionLabels(string section) => section switch
    {
        "streets" => ("Street", "الشارع"),
        "meter" => ("Meter", "العداد"),
        "property" => ("Property", "بيانات العقار"),
        "electricity_block" => ("Electricity", "الكهرباء"),
        "building" => ("Building", "المبنى"),
        "location" => ("Location", "الموقع"),
        _ => (section, section),
    };

    private static IReadOnlyList<FieldSpec> LoadFields()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(FieldsResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{FieldsResourceName}'.");

        var fields = JsonSerializer.Deserialize<List<FieldSpec>>(stream, FieldJsonOptions)
            ?? throw new InvalidOperationException("Eastern main-streets field spec deserialized to null.");

        if (fields.Count == 0)
        {
            throw new InvalidOperationException("Eastern main-streets field spec is empty.");
        }

        return fields;
    }

    private static string BuildSnapshotJson(SurveyTemplate template) =>
        $$"""{"templateCode":"{{template.TemplateCode}}","templateNameEn":"{{template.TemplateNameEn}}","templateNameAr":"{{template.TemplateNameAr}}","category":"{{template.Category}}"}""";

    private sealed class FieldSpec
    {
        public string DataName { get; set; } = "";
        public string Type { get; set; } = "";
        public string LabelEn { get; set; } = "";
        public string LabelAr { get; set; } = "";
        public bool Required { get; set; }
        public string Section { get; set; } = "";
        public string? NumericFormat { get; set; }
        public int? MaxFiles { get; set; }
        public bool AllowOther { get; set; }
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public List<ChoiceSpec>? Choices { get; set; }
    }

    private sealed class ChoiceSpec
    {
        public string Value { get; set; } = "";
        public string LabelEn { get; set; } = "";
        public string LabelAr { get; set; } = "";
    }
}
