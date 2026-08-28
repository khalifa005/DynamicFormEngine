namespace KH.Domain.Constants.Fsms;

/// <summary>
/// External systems a historical data set can be imported from. The code selects which
/// <c>IMigrationSourceAdapter</c> reads the uploaded file, so nothing about the pipeline that
/// drives an import is specific to any one of them — a second source is a new adapter and a new
/// code here, nothing more.
/// </summary>
public abstract class MigrationSourceCodes
{
    /// <summary>Fulcrum app export (Excel workbook + a flat folder of <c>{uuid}.jpg</c> media).</summary>
    public const string Fulcrum = "FULCRUM";

    public static readonly IReadOnlyList<string> All =
    [
        Fulcrum
    ];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
