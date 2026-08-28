namespace KH.Domain.Constants.Fsms;

/// <summary>
/// How a <c>numeric</c> field's answer is written. Mirrors <c>NUMERIC_FORMATS</c> in the Angular
/// builder (<c>form-builder.types.ts</c>).
/// </summary>
public abstract class SurveyNumericFormats
{
    public const string Decimal = "decimal";

    /// <summary>Whole numbers only — the fill form adds an <c>integer</c> validator for it.</summary>
    public const string Integer = "integer";
}
