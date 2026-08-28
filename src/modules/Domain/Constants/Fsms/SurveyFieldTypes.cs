namespace KH.Domain.Constants.Fsms;

/// <summary>
/// Authoritative allow-list of supported survey field types (spec 5.3). The set mirrors the WFM
/// Formly reference schema so a template can only declare a field type the client renderers
/// understand. Owned by task-02 (form engine / schema validation).
/// </summary>
public abstract class SurveyFieldTypes
{
    // --- Text ---
    public const string Input = "input";
    public const string CustomInput = "custom-input";
    public const string Textarea = "textarea";
    public const string CustomTextarea = "custom-textarea";

    // --- Numeric ---
    public const string Integer = "integer";
    public const string CustomNumber = "custom-number";

    // --- Choice / lookup ---
    public const string Select = "select";
    public const string AutocompleteEm = "autocompleteem";
    public const string AutocompleteMultiSelect = "autocompleteMultiSelect";

    // --- Date / time ---
    public const string DatePicker = "datepicker";

    public const string CustomDatePicker = "customdatepicker";
    public const string CustomDate = "custom-date";
    public const string DateTimePicker = "datetimepicker";
    public const string NewDateTimePicker = "new-datetime-picker";
    public const string CustomDateTime = "custom-datetime";
    public const string CustomTime = "custom-time";

    // --- File ---
    public const string File = "file";

    // --- Additional NWC field types (superset beyond the WFM reference 18) ---
    public const string Toggle = "toggle";
    public const string FileUpload = "fileupload";
    public const string Geolocation = "geolocation";
    public const string Autocomplete = "autocomplete";
    public const string MultiCheckbox = "multicheckbox";
    public const string Checkbox = "checkbox";
    public const string Radio = "radio";

    // --- Layout ---
    public const string FieldGroup = "fieldGroup";

    /// <summary>
    /// System/hidden field. Stored with this sentinel so the column stays non-null, but the assembler
    /// projects it as <c>"type": null</c> to match the reference schema (e.g. <c>FBFId</c>, <c>PK</c>,
    /// <c>TaskCode</c>).
    /// </summary>
    public const string Hidden = "hidden";

    public static readonly IReadOnlyList<string> All =
    [
        Input,
        CustomInput,
        Textarea,
        CustomTextarea,
        Integer,
        CustomNumber,
        Select,
        AutocompleteEm,
        AutocompleteMultiSelect,
        DatePicker,
        CustomDatePicker,
        CustomDate,
        DateTimePicker,
        NewDateTimePicker,
        CustomDateTime,
        CustomTime,
        File,
        Toggle,
        FileUpload,
        Geolocation,
        Autocomplete,
        MultiCheckbox,
        Checkbox,
        Radio,
        FieldGroup,
        Hidden
    ];

    /// <summary>Field types that carry a selectable option set (static rows or a shared lookup).</summary>
    public static readonly IReadOnlyList<string> OptionBearing =
    [
        Select,
        AutocompleteEm,
        AutocompleteMultiSelect,
        Autocomplete,
        MultiCheckbox,
        Radio
    ];

    /// <summary>Field types that hold nested fields rather than a value.</summary>
    public static bool IsLayout(string? value) =>
        string.Equals(value, FieldGroup, StringComparison.Ordinal);

    /// <summary>Field types that are not rendered as an interactive control (emitted as null type).</summary>
    public static bool IsHidden(string? value) =>
        string.Equals(value, Hidden, StringComparison.Ordinal);

    public static bool SupportsOptions(string? value) =>
        value is not null && OptionBearing.Contains(value, StringComparer.Ordinal);

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
