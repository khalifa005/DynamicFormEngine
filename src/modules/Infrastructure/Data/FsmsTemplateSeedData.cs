using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Templates;
using Microsoft.EntityFrameworkCore;

namespace KH.Infrastructure.Data;

/// <summary>
/// Seeds demo survey templates using the form-builder "Fulcrum-style" definition JSON
/// (<c>name_en</c>/<c>name_ar</c>/<c>elements</c>) stored on <see cref="SurveyTemplate.DefinitionJson"/>.
/// </summary>
internal static class FsmsTemplateSeedData
{
    internal const string DemoTemplateCode = "SRV-DEMO-ALL-TYPES";
    internal const string PublishedTemplateCode = "SRV-WATER-LEAK-001";
    internal const string FieldSurveyTemplateCode = "SRV-FIELD-SURVEY-001";
    internal const string MsFormsSurveyTemplateCode = "SRV-MSFORMS-SURVEY-2026";

    /// <summary>The FA type the published seed template answers for (see <c>FsmsSeedData</c>).</summary>
    private const string WaterLeakFaTypeCode = "F-INVEST";

    internal static async Task SeedTemplatesAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        await SeedDemoTemplateAsync(context, cancellationToken);
        await SeedPublishedWaterLeakTemplateAsync(context, cancellationToken);
        await SeedPublishedFieldSurveyTemplateAsync(context, cancellationToken);
        await SeedPublishedMsFormsSurveyTemplateAsync(context, cancellationToken);
        await FsmsEasternMainStreetsTemplateSeedData.SeedAsync(context, cancellationToken);
        await FsmsStationSurveyTemplateSeedData.SeedAsync(context, cancellationToken);
    }

    private static async Task SeedDemoTemplateAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        if (await context.SurveyTemplates.AnyAsync(x => x.TemplateCode == DemoTemplateCode, cancellationToken))
        {
            return;
        }

        var template = SurveyTemplate.Create(
            DemoTemplateCode,
            "FSMS Demo — All Field Types",
            "نموذج تجريبي — جميع أنواع الحقول",
            SurveyCategories.Custom,
            teamFillSlaHours: 72,
            completionSlaHours: 48,
            departmentId: 10,
            branchScope: "1110,2200,RCBU");

        template.SetDefinition(DemoDefinitionJson, "FSMS Demo — All Field Types", "نموذج تجريبي — جميع أنواع الحقول");

        context.SurveyTemplates.Add(template);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedPublishedWaterLeakTemplateAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        if (await context.SurveyTemplates.AnyAsync(x => x.TemplateCode == PublishedTemplateCode, cancellationToken))
        {
            return;
        }

        var template = SurveyTemplate.Create(
            PublishedTemplateCode,
            "Water Leakage Inspection (F-INVEST)",
            "فحص تسرب عداد مياه",
            SurveyCategories.AssetInspection,
            teamFillSlaHours: 72,
            completionSlaHours: 48,
            departmentId: 10,
            branchScope: "1110,RCBU",
            // Matches the seeded FA type, so the inbound feed can resolve this template by type alone.
            faTypeCode: WaterLeakFaTypeCode);

        template.SetDefinition(WaterLeakDefinitionJson, "Water Leakage Inspection (F-INVEST)", "فحص تسرب عداد مياه");

        context.SurveyTemplates.Add(template);
        await context.SaveChangesAsync(cancellationToken);

        // Freeze a single client-agnostic snapshot (the frozen builder definition).
        var snapshots = new[]
        {
            new TemplateVersionSnapshot(TargetClients.Formly, template.DefinitionJson, BuildTemplateSnapshotJson(template)),
        };

        template.Publish("seed", snapshots, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedPublishedFieldSurveyTemplateAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        var existing = await context.SurveyTemplates
            .FirstOrDefaultAsync(x => x.TemplateCode == FieldSurveyTemplateCode, cancellationToken);

        if (existing is null)
        {
            var template = SurveyTemplate.Create(
                FieldSurveyTemplateCode,
                "Field Survey",
                "المسح الميداني",
                SurveyCategories.AssetInspection,
                teamFillSlaHours: 72,
                completionSlaHours: 48,
                departmentId: 10,
                branchScope: "1110,RCBU");

            template.SetDefinition(FieldSurveyDefinitionJson, "Field Survey", "المسح الميداني");

            context.SurveyTemplates.Add(template);
            await context.SaveChangesAsync(cancellationToken);

            PublishFieldSurvey(template);
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!NeedsFieldSurveyDefinitionRefresh(existing.DefinitionJson))
        {
            return;
        }

        existing.SetDefinition(FieldSurveyDefinitionJson, "Field Survey", "المسح الميداني");
        PublishFieldSurvey(existing);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedPublishedMsFormsSurveyTemplateAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        if (await context.SurveyTemplates.AnyAsync(x => x.TemplateCode == MsFormsSurveyTemplateCode, cancellationToken))
        {
            return;
        }

        var template = SurveyTemplate.Create(
            MsFormsSurveyTemplateCode,
            "Field Survey Initiative 2026",
            "مبادرة المسح الميداني 2026",
            SurveyCategories.AssetInspection,
            teamFillSlaHours: 72,
            completionSlaHours: 48,
            departmentId: 10,
            branchScope: "1110,RCBU");

        template.SetDefinition(MsFormsSurveyDefinitionJson, "Field Survey Initiative 2026", "مبادرة المسح الميداني 2026");

        context.SurveyTemplates.Add(template);
        await context.SaveChangesAsync(cancellationToken);

        var snapshots = new[]
        {
            new TemplateVersionSnapshot(TargetClients.Formly, template.DefinitionJson, BuildTemplateSnapshotJson(template)),
        };

        template.Publish("seed", snapshots, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void PublishFieldSurvey(SurveyTemplate template)
    {
        var snapshots = new[]
        {
            new TemplateVersionSnapshot(TargetClients.Formly, template.DefinitionJson, BuildTemplateSnapshotJson(template)),
        };

        template.Publish("seed", snapshots, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// True when the stored seed still has English-as-Arabic placeholders (or comments as <c>text</c>).
    /// Does not overwrite a designer-edited definition that already has the Arabic labels.
    /// </summary>
    private static bool NeedsFieldSurveyDefinitionRefresh(string? definitionJson)
    {
        if (string.IsNullOrWhiteSpace(definitionJson))
        {
            return true;
        }

        return definitionJson.Contains("\"label_ar\": \"comments\"", StringComparison.Ordinal)
            || definitionJson.Contains("\"label_ar\": \"task\"", StringComparison.Ordinal)
            || definitionJson.Contains("\"label_ar\": \"dma\"", StringComparison.Ordinal)
            || definitionJson.Contains("\"label_ar\": \"remarks2\"", StringComparison.Ordinal);
    }

    private static string BuildTemplateSnapshotJson(SurveyTemplate template) => $$"""
        {"templateCode":"{{template.TemplateCode}}","templateNameEn":"{{template.TemplateNameEn}}","templateNameAr":"{{template.TemplateNameAr}}","category":"{{template.Category}}"}
        """;

    private const string DemoDefinitionJson = """
    {
      "name_en": "FSMS Demo — All Field Types",
      "name_ar": "نموذج تجريبي — جميع أنواع الحقول",
      "elements": [
        { "type": "text", "label_en": "Full Name", "label_ar": "الاسم الكامل", "data_name": "full_name", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": true, "disabled": false, "min_length": 3, "max_length": 100, "pattern": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "numeric", "label_en": "Age", "label_ar": "العمر", "data_name": "age", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "format": "integer", "min": 0, "max": 120, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "yes_no", "label_en": "Has Vehicle", "label_ar": "لديه مركبة", "data_name": "has_vehicle", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "date", "label_en": "Inspection Date", "label_ar": "تاريخ المعاينة", "data_name": "inspection_date", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "single_choice", "label_en": "Sport", "label_ar": "الرياضة", "data_name": "sport", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "allow_other": true, "multiple": false, "parent_field": null, "choices": [ { "value": "soccer", "label_en": "Soccer", "label_ar": "كرة القدم", "dependency_value": null }, { "value": "basketball", "label_en": "Basketball", "label_ar": "كرة السلة", "dependency_value": null } ], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "geolocation", "label_en": "Site Location", "label_ar": "موقع الموقع", "data_name": "site_location", "description_en": "", "description_ar": "", "hidden": false, "default_value": "24.7136,46.6753", "required": false, "disabled": false, "map_zoom": 6, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } }
      ]
    }
    """;

    private const string WaterLeakDefinitionJson = """
    {
      "name_en": "Water Leakage Inspection (F-INVEST)",
      "name_ar": "فحص تسرب عداد مياه",
      "elements": [
        { "type": "text", "label_en": "Field Activity ID", "label_ar": "معرف النشاط الميداني", "data_name": "field_activity_id", "description_en": "", "description_ar": "", "hidden": false, "default_value": "91504205924954", "required": true, "disabled": false, "min_length": null, "max_length": null, "pattern": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "text", "label_en": "Meter Number", "label_ar": "رقم العداد", "data_name": "meter_number", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "min_length": null, "max_length": null, "pattern": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "single_choice", "label_en": "Leak Severity", "label_ar": "شدة التسرب", "data_name": "leak_severity", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": true, "disabled": false, "allow_other": false, "multiple": false, "parent_field": null, "choices": [ { "value": "low", "label_en": "Low", "label_ar": "منخفض", "dependency_value": null }, { "value": "med", "label_en": "Medium", "label_ar": "متوسط", "dependency_value": null }, { "value": "high", "label_en": "High", "label_ar": "مرتفع", "dependency_value": null } ], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "geolocation", "label_en": "On-site GPS", "label_ar": "الموقع الجغرافي", "data_name": "gps_capture", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": true, "disabled": false, "map_zoom": 8, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } }
      ]
    }
    """;

    /// <summary>
    /// Fulcrum المسح الميداني. Keys match the export columns — see
    /// Docs/business-logic/fulcrum-field-survey-mapping.md.
    /// </summary>
    private const string FieldSurveyDefinitionJson = """
    {
      "name_en": "Field Survey",
      "name_ar": "المسح الميداني",
      "elements": [
        {
          "type": "section",
          "label_en": "Property Data",
          "label_ar": "بيانات العقار",
          "data_name": "parcel_data",
          "description_en": "",
          "description_ar": "",
          "hidden": false,
          "display": "inline",
          "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false },
          "elements": [
            { "type": "single_choice", "label_en": "Neighborhoods", "label_ar": "الاحياء", "data_name": "area", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "allow_other": false, "multiple": false, "parent_field": null, "choices": [ { "value": "الاسكان", "label_en": "Al-Iskan", "label_ar": "الاسكان", "dependency_value": null } ], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "single_choice", "label_en": "Property Type", "label_ar": "نوع العقار", "data_name": "parcel_type", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "allow_other": false, "multiple": false, "parent_field": null, "choices": [ { "value": "فيلا", "label_en": "Villa", "label_ar": "فيلا", "dependency_value": null }, { "value": "عمارة", "label_en": "Building", "label_ar": "عمارة", "dependency_value": null }, { "value": "مبني حكومي", "label_en": "Government building", "label_ar": "مبني حكومي", "dependency_value": null }, { "value": "مسجد", "label_en": "Mosque", "label_ar": "مسجد", "dependency_value": null }, { "value": "مدرسة خاصة", "label_en": "Private school", "label_ar": "مدرسة خاصة", "dependency_value": null }, { "value": "مركز تجاري", "label_en": "Commercial center", "label_ar": "مركز تجاري", "dependency_value": null } ], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "single_choice", "label_en": "Consumption Type", "label_ar": "نوع الاستهلاك", "data_name": "consumption_type", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "allow_other": false, "multiple": false, "parent_field": null, "choices": [ { "value": "سكني", "label_en": "Residential", "label_ar": "سكني", "dependency_value": null }, { "value": "تجاري", "label_en": "Commercial", "label_ar": "تجاري", "dependency_value": null }, { "value": "حكومي", "label_en": "Government", "label_ar": "حكومي", "dependency_value": null } ], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "numeric", "label_en": "Building Number", "label_ar": "رقم المبني", "data_name": "spl_building_no", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "format": "integer", "min": null, "max": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "numeric", "label_en": "Number of Floors", "label_ar": "عدد الادوار", "data_name": "parcel_floors_count", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "format": "integer", "min": null, "max": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "numeric", "label_en": "Number of Units", "label_ar": "عدد الوحدات", "data_name": "parcel_units_count", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "format": "integer", "min": null, "max": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "photo", "label_en": "Property Photos", "label_ar": "صور العقار", "data_name": "parcel_photos", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "max_files": 10, "max_file_size_mb": 10, "allowed_extensions": ["jpg", "jpeg", "png", "heic", "webp"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } }
          ]
        },
        {
          "type": "section",
          "label_en": "Drainage",
          "label_ar": "الصرف",
          "data_name": "sewer",
          "description_en": "",
          "description_ar": "",
          "hidden": false,
          "display": "inline",
          "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false },
          "elements": [
            { "type": "yes_no", "label_en": "Sewage connection exists", "label_ar": "يوجد توصيلة صرف", "data_name": "sewer_connection_exists", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "photo", "label_en": "Sewage connection photos", "label_ar": "صور توصيلة الصرف الصحي", "data_name": "sewer_connection_photos", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "max_files": 10, "max_file_size_mb": 10, "allowed_extensions": ["jpg", "jpeg", "png", "heic", "webp"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } }
          ]
        },
        { "type": "yes_no", "label_en": "Water connection exists", "label_ar": "يوجد توصيلة مياه", "data_name": "water_connection_exists", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "yes_no", "label_en": "Property Status", "label_ar": "حالة العقار", "data_name": "_parcel_working_case", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "single_choice", "label_en": "Connection Type", "label_ar": "نوع التوصيلة", "data_name": "water_house_connection_type", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "allow_other": false, "multiple": false, "parent_field": null, "choices": [ { "value": "عداد", "label_en": "Meter", "label_ar": "عداد", "dependency_value": null }, { "value": "وصلة  مباشرة", "label_en": "Direct connection", "label_ar": "وصلة  مباشرة", "dependency_value": null } ], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "single_choice", "label_en": "Direct connection coordinate", "label_ar": "احداثية الوصلة المباشرة", "data_name": "_visible_dc_status", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "allow_other": false, "multiple": false, "parent_field": null, "choices": [], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "photo", "label_en": "Direct connection photos", "label_ar": "صور الوصلة المباشرة", "data_name": "direct_connection_photos", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "max_files": 10, "max_file_size_mb": 10, "allowed_extensions": ["jpg", "jpeg", "png", "heic", "webp"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "photo", "label_en": "Hidden connection photos", "label_ar": "صور الوصلة غير ظاهرة", "data_name": "hidden_connection_photos", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "max_files": 10, "max_file_size_mb": 10, "allowed_extensions": ["jpg", "jpeg", "png", "heic", "webp"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        {
          "type": "section",
          "label_en": "Meter Data",
          "label_ar": "بيانات العدادات",
          "data_name": "meter_data",
          "description_en": "",
          "description_ar": "",
          "hidden": false,
          "display": "inline",
          "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false },
          "elements": [
            { "type": "numeric", "label_en": "Number of water meters", "label_ar": "عدد عدادات المياه", "data_name": "parcel_water_meters_count", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "format": "integer", "min": null, "max": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "single_choice", "label_en": "Meter Status", "label_ar": "حالة العداد", "data_name": "water_meter_status", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "allow_other": false, "multiple": false, "parent_field": null, "choices": [ { "value": "يعمل", "label_en": "Working", "label_ar": "يعمل", "dependency_value": null }, { "value": "عداد مزال", "label_en": "Meter removed", "label_ar": "عداد مزال", "dependency_value": null } ], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "barcode", "label_en": "Meter Number", "label_ar": "رقم العداد", "data_name": "water_meter_serial_number", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "barcode_formats": ["qr_code", "code_128", "code_39", "ean_13", "ean_8", "upc_a"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "photo", "label_en": "Meter Photo", "label_ar": "صورة العداد", "data_name": "water_meter_photos", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "max_files": 10, "max_file_size_mb": 10, "allowed_extensions": ["jpg", "jpeg", "png", "heic", "webp"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "single_choice", "label_en": "Meter Type", "label_ar": "نوع العداد", "data_name": "water_meter_factory_type", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "allow_other": false, "multiple": false, "parent_field": null, "choices": [ { "value": "هيدروس", "label_en": "Hydrus", "label_ar": "هيدروس", "dependency_value": null }, { "value": "سينسوس", "label_en": "Sensus", "label_ar": "سينسوس", "dependency_value": null }, { "value": "وداد", "label_en": "Widad", "label_ar": "وداد", "dependency_value": null } ], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "single_choice", "label_en": "Meter Location", "label_ar": "موقع العداد", "data_name": "water_meter_location", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "allow_other": false, "multiple": false, "parent_field": null, "choices": [ { "value": "جداري", "label_en": "Wall-mounted", "label_ar": "جداري", "dependency_value": null } ], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "numeric", "label_en": "Inventory number", "label_ar": "رقم الحصر", "data_name": "hcn_number", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "format": "decimal", "min": null, "max": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "photo", "label_en": "Inventory number photos", "label_ar": "صور رقم الحصر", "data_name": "hcn_photo", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "max_files": 10, "max_file_size_mb": 10, "allowed_extensions": ["jpg", "jpeg", "png", "heic", "webp"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "barcode", "label_en": "NWC Plate Number", "label_ar": "رقم لوحة نوك", "data_name": "nwc_plate_number", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "barcode_formats": ["qr_code", "code_128", "code_39", "ean_13", "ean_8", "upc_a"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "photo", "label_en": "NWC Plate Photos", "label_ar": "صور لوحة نوك", "data_name": "nwc_plate_photo", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "max_files": 10, "max_file_size_mb": 10, "allowed_extensions": ["jpg", "jpeg", "png", "heic", "webp"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } }
          ]
        },
        {
          "type": "section",
          "label_en": "Electricity",
          "label_ar": "الكهرباء",
          "data_name": "electricity",
          "description_en": "",
          "description_ar": "",
          "hidden": false,
          "display": "inline",
          "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false },
          "elements": [
            { "type": "numeric", "label_en": "Number of electricity meters", "label_ar": "عدد عدادات الكهرباء", "data_name": "number_of_electricity_meters", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "format": "integer", "min": null, "max": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "numeric", "label_en": "Electricity meter number", "label_ar": "رقم عداد الكهرباء", "data_name": "one_of_electricity_meters_number", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "format": "integer", "min": null, "max": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "photo", "label_en": "Electricity meter photos", "label_ar": "صور عدادات الكهرباء", "data_name": "electricity_meters_photos", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "max_files": 10, "max_file_size_mb": 10, "allowed_extensions": ["jpg", "jpeg", "png", "heic", "webp"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } }
          ]
        },
        { "type": "yes_no", "label_en": "Unregistered sewer connection", "label_ar": "وصلة صرف غير مسجلة", "data_name": "_unregistered_sewer_connection", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "photo", "label_en": "Unregistered sewer connection photos", "label_ar": "صور وصلة صرف غير مسجلة", "data_name": "__unregistered_sewer_connection_photos", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "max_files": 10, "max_file_size_mb": 10, "allowed_extensions": ["jpg", "jpeg", "png", "heic", "webp"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "video", "label_en": "Unregistered sewer connection video", "label_ar": "فيديو وصلة صرف غير مسجلة", "data_name": "___unregistered_sewer_connection_videos", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "max_files": 1, "max_file_size_mb": 50, "allowed_extensions": ["mp4", "mov", "webm"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "memo", "label_en": "Comments", "label_ar": "التعليقات", "data_name": "comments", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "min_length": null, "max_length": null, "pattern": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "text", "label_en": "Remarks", "label_ar": "ملاحظات", "data_name": "remarks", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "min_length": null, "max_length": null, "pattern": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "geolocation", "label_en": "Address", "label_ar": "العنوان", "data_name": "address", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "map_zoom": 8, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "yes_no", "label_en": "Surveying completed", "label_ar": "تم الرفع المساحي", "data_name": "_survey", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "numeric", "label_en": "Task", "label_ar": "المهمة", "data_name": "task", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "format": "integer", "min": null, "max": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "text", "label_en": "DMA", "label_ar": "رمز DMA", "data_name": "dma", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "min_length": null, "max_length": null, "pattern": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
        { "type": "memo", "label_en": "Additional Remarks", "label_ar": "ملاحظات إضافية", "data_name": "remarks2", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "min_length": null, "max_length": null, "pattern": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } }
      ]
    }
    """;

    /// <summary>
    /// Microsoft Forms مبادرة المسح الميداني 2026. English <c>data_name</c>s mapped from Arabic
    /// headers — see Docs/business-logic/msforms-field-survey-2026-mapping.md.
    /// </summary>
    private const string MsFormsSurveyDefinitionJson = """
    {
      "name_en": "Field Survey Initiative 2026",
      "name_ar": "مبادرة المسح الميداني 2026",
      "elements": [
        {
          "type": "section",
          "label_en": "Property",
          "label_ar": "بيانات العقار",
          "data_name": "property",
          "description_en": "",
          "description_ar": "",
          "hidden": false,
          "display": "inline",
          "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false },
          "elements": [
            { "type": "text", "label_en": "Street name", "label_ar": "اسم الشارع", "data_name": "street_name", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "min_length": null, "max_length": null, "pattern": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "text", "label_en": "Property name", "label_ar": "اسم العقار إن وجد", "data_name": "property_name", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "min_length": null, "max_length": null, "pattern": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "single_choice", "label_en": "Property classification", "label_ar": "تصنيف العقار", "data_name": "property_classification", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "allow_other": false, "multiple": false, "parent_field": null, "choices": [ { "value": "سكني", "label_en": "Residential", "label_ar": "سكني", "dependency_value": null }, { "value": "تجاري", "label_en": "Commercial", "label_ar": "تجاري", "dependency_value": null } ], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "single_choice", "label_en": "Easement status", "label_ar": "حالة الارتفاق", "data_name": "easement_status", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "allow_other": false, "multiple": false, "parent_field": null, "choices": [ { "value": "مياه وصرف صحي", "label_en": "Water and sewage", "label_ar": "مياه وصرف صحي", "dependency_value": null } ], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "numeric", "label_en": "Number of Floors", "label_ar": "عدد الطوابق", "data_name": "parcel_floors_count", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "format": "integer", "min": null, "max": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "numeric", "label_en": "Number of shops", "label_ar": "عدد المحلات التجارية", "data_name": "shop_count", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "format": "integer", "min": null, "max": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "photo", "label_en": "Property photo", "label_ar": "صورة العقار", "data_name": "property_photo", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "max_files": 10, "max_file_size_mb": 10, "allowed_extensions": ["jpg", "jpeg", "png", "heic", "webp"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "photo", "label_en": "Extra photo", "label_ar": "صورة إضافية", "data_name": "extra_photo", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "max_files": 10, "max_file_size_mb": 10, "allowed_extensions": ["jpg", "jpeg", "png", "heic", "webp"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } }
          ]
        },
        {
          "type": "section",
          "label_en": "Meter",
          "label_ar": "العداد",
          "data_name": "meter",
          "description_en": "",
          "description_ar": "",
          "hidden": false,
          "display": "inline",
          "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false },
          "elements": [
            { "type": "text", "label_en": "Meter Number", "label_ar": "رقم العداد", "data_name": "meter_no", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "min_length": null, "max_length": null, "pattern": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "photo", "label_en": "Indoor meter photo", "label_ar": "صورة العداد ( الداخلي )", "data_name": "meter_photo_indoor", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "max_files": 10, "max_file_size_mb": 10, "allowed_extensions": ["jpg", "jpeg", "png", "heic", "webp"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "photo", "label_en": "Outdoor meter photo", "label_ar": "صورة العداد ( الخارجي )", "data_name": "meter_photo_outdoor", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "max_files": 10, "max_file_size_mb": 10, "allowed_extensions": ["jpg", "jpeg", "png", "heic", "webp"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "text", "label_en": "Property connection number", "label_ar": "رقم توصيلة العقار", "data_name": "property_connection_no", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "min_length": null, "max_length": null, "pattern": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } }
          ]
        },
        {
          "type": "section",
          "label_en": "Electricity",
          "label_ar": "الكهرباء",
          "data_name": "electricity_block",
          "description_en": "",
          "description_ar": "",
          "hidden": false,
          "display": "inline",
          "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false },
          "elements": [
            { "type": "numeric", "label_en": "Number of electricity meters", "label_ar": "عدد عدادات الكهرباء", "data_name": "number_of_electricity_meters", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "format": "integer", "min": null, "max": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "photo", "label_en": "Electricity meter photos", "label_ar": "صور عدادات الكهرباء", "data_name": "electricity_meters_photos", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "max_files": 10, "max_file_size_mb": 10, "allowed_extensions": ["jpg", "jpeg", "png", "heic", "webp"], "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } }
          ]
        },
        {
          "type": "section",
          "label_en": "Location",
          "label_ar": "الموقع",
          "data_name": "location",
          "description_en": "",
          "description_ar": "",
          "hidden": false,
          "display": "inline",
          "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false },
          "elements": [
            { "type": "text", "label_en": "Geographic location", "label_ar": "الموقع الجغرافي", "data_name": "maps_url", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "min_length": null, "max_length": null, "pattern": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } },
            { "type": "text", "label_en": "Remarks", "label_ar": "ملاحظات", "data_name": "remarks", "description_en": "", "description_ar": "", "hidden": false, "default_value": null, "required": false, "disabled": false, "min_length": null, "max_length": null, "pattern": null, "visible_conditions": { "match": "all", "conditions": [], "preserve_data": false }, "required_conditions": { "match": "all", "conditions": [], "preserve_data": false } }
          ]
        }
      ]
    }
    """;
}
