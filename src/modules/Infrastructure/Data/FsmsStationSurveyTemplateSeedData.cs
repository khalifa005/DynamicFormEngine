using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Templates;
using Microsoft.EntityFrameworkCore;

namespace KH.Infrastructure.Data;

/// <summary>
/// Seeds <c>SRV-STATION-SURVEY-001</c> from the bilingual filling-station spec
/// (المسح الميداني نهائي). Field list lives in <c>FsmsStationSurveyFields.json</c>.
/// See Docs/business-logic/station-survey-final-mapping.md.
/// </summary>
internal static class FsmsStationSurveyTemplateSeedData
{
    internal const string TemplateCode = "SRV-STATION-SURVEY-001";

    private const string NameEn = "Field Survey — Final";
    private const string NameAr = "المسح الميداني نهائي";
    private const string FieldsResourceName = "KH.Infrastructure.Data.FsmsStationSurveyFields.json";

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
                obj["format"] = field.NumericFormat ?? "decimal";
                obj["min"] = null;
                obj["max"] = null;
                break;
            case "single_choice":
                obj["allow_other"] = false;
                obj["multiple"] = false;
                obj["parent_field"] = null;
                obj["choices"] = BuildChoices(field.Choices);
                break;
            case "photo":
                obj["max_files"] = field.MaxFiles ?? 10;
                obj["max_file_size_mb"] = 10;
                obj["allowed_extensions"] = new JsonArray(PhotoExtensions.Select(x => JsonValue.Create(x)).ToArray());
                break;
        }

        obj["visible_conditions"] = BuildRuleGroup(field.VisMatch, field.Visible);
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

    private static JsonObject BuildRuleGroup(string? match, IReadOnlyList<VisSpec>? conditions)
    {
        var array = new JsonArray();
        if (conditions is not null)
        {
            foreach (var condition in conditions)
            {
                if (string.IsNullOrWhiteSpace(condition.Field))
                {
                    continue;
                }

                array.Add(new JsonObject
                {
                    ["field"] = condition.Field,
                    ["operator"] = string.IsNullOrWhiteSpace(condition.Op) ? "equal" : condition.Op,
                    ["value"] = condition.Value ?? "",
                });
            }
        }

        var ruleMatch = string.Equals(match, "any", StringComparison.OrdinalIgnoreCase) ? "any" : "all";
        return new JsonObject
        {
            ["match"] = ruleMatch,
            ["conditions"] = array,
            ["preserve_data"] = false,
        };
    }

    private static JsonObject EmptyRuleGroup() => new()
    {
        ["match"] = "all",
        ["conditions"] = new JsonArray(),
        ["preserve_data"] = false,
    };

    private static (string En, string Ar) SectionLabels(string section) => section switch
    {
        "station" => ("Station", "المحطة"),
        "supply" => ("Supply", "التغذية"),
        "meter" => ("Meter", "العداد"),
        "access" => ("Access", "المداخل والمخارج"),
        "yard" => ("Yard", "الساحات"),
        "filling_points" => ("Filling Points", "المناهل"),
        "utilities" => ("Utilities", "المرافق"),
        "site_services" => ("Site Services", "خدمات الموقع"),
        _ => (section, section),
    };

    private static IReadOnlyList<FieldSpec> LoadFields()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(FieldsResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{FieldsResourceName}'.");

        var fields = JsonSerializer.Deserialize<List<FieldSpec>>(stream, FieldJsonOptions)
            ?? throw new InvalidOperationException("Station survey field spec deserialized to null.");

        if (fields.Count == 0)
        {
            throw new InvalidOperationException("Station survey field spec is empty.");
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
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public string VisMatch { get; set; } = "all";
        public List<ChoiceSpec>? Choices { get; set; }
        public List<VisSpec>? Visible { get; set; }
    }

    private sealed class ChoiceSpec
    {
        public string Value { get; set; } = "";
        public string LabelEn { get; set; } = "";
        public string LabelAr { get; set; } = "";
    }

    private sealed class VisSpec
    {
        public string Field { get; set; } = "";
        public string Op { get; set; } = "equal";
        public string Value { get; set; } = "";
    }
}
