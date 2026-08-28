namespace KH.Domain.Constants.Fsms;

/// <summary>
/// Authoritative allow-list of supported field validator names (spec 5.4.2). Each name has a declared
/// option-shape that <c>SurveySchemaValidator</c> enforces at publish, and a mirrored server-side
/// answer validator (task-06). Owned by task-02.
/// </summary>
public abstract class SurveyValidatorNames
{
    public const string Required = "required";
    public const string Pattern = "pattern";
    public const string Min = "min";
    public const string Max = "max";
    public const string MinLength = "minLength";
    public const string MaxLength = "maxLength";
    public const string CustomDate = "custom-date";
    public const string DateTimeValidation = "datetime-validation";
    public const string DateFuture = "date-future";
    public const string AutocompleteValidation = "autocomplete-validation";
    public const string AllowedFileExtensions = "allowed-file-extensions";
    public const string TotalFileSize = "total-file-size";
    public const string MaxFiles = "max-files";

    public static readonly IReadOnlyList<string> All =
    [
        Required,
        Pattern,
        Min,
        Max,
        MinLength,
        MaxLength,
        CustomDate,
        DateTimeValidation,
        DateFuture,
        AutocompleteValidation,
        AllowedFileExtensions,
        TotalFileSize,
        MaxFiles
    ];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
