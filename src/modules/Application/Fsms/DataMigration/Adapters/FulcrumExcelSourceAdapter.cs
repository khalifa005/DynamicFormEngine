using System.Globalization;
using ClosedXML.Excel;
using KH.Application.Fsms.Common.Definition;
using KH.Application.Fsms.DataMigration.Common;
using KH.Domain.Constants.Fsms;

namespace KH.Application.Fsms.DataMigration.Adapters;

/// <summary>
/// Reads a Fulcrum app export workbook. The seed described in
/// <c>Docs/business-logic/fulcrum-field-survey-mapping.md</c> took every <c>data_name</c> verbatim
/// from this file's column names, which is what lets this adapter map by name alone rather than
/// carrying a translation table.
///
/// Everything Fulcrum-specific about the import lives here:
/// <list type="bullet">
/// <item>The main sheet is found by <em>index</em>. Its name is the app id, which Excel truncates to
/// 31 characters, so matching on a name would fail for every export.</item>
/// <item>Sheets after the first are per-media sidecars holding EXIF and file sizes. The bytes come
/// from the export's media folder instead, so they are not read.</item>
/// <item><c>*_caption</c> and <c>*_url</c> are export noise beside each media column — the caption
/// is usually a bare comma and the URL is a Fulcrum web-viewer link, not a file.</item>
/// <item>Fulcrum's Address explodes into nine <c>address_*</c> columns. We model one geolocation
/// field, composed from the record's own latitude/longitude plus <c>address_full</c>.</item>
/// <item><c>yes</c> / <c>no</c> are passed through untouched — the submission store already reads
/// both when writing a BIT column.</item>
/// </list>
/// </summary>
public sealed class FulcrumExcelSourceAdapter : IMigrationSourceAdapter
{
    private const string ExternalIdColumn = "fulcrum_id";
    private const string CreatedAtColumn = "created_at";
    private const string CreatedByColumn = "created_by";
    private const string StatusColumn = "status";
    private const string LatitudeColumn = "latitude";
    private const string LongitudeColumn = "longitude";
    private const string AddressFullColumn = "address_full";

    /// <summary>The one geolocation field the nine exploded <c>address_*</c> columns collapse into.</summary>
    private const string GeolocationDataName = "address";

    /// <summary>Fulcrum's word for a record its reviewer closed. Anything else is still in flight.</summary>
    private const string ApprovedStatus = "Approved";

    private const char MediaKeySeparator = ',';

    private const string CaptionSuffix = "_caption";
    private const string UrlSuffix = "_url";

    /// <summary>
    /// Columns that exist in every export and are never answers: Fulcrum's own bookkeeping, the
    /// PostGIS geometry (the same point as latitude/longitude), and the exploded address parts.
    /// </summary>
    private static readonly HashSet<string> SystemColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ExternalIdColumn,
        CreatedAtColumn,
        CreatedByColumn,
        StatusColumn,
        LatitudeColumn,
        LongitudeColumn,
        "updated_at",
        "updated_by",
        "system_created_at",
        "system_updated_at",
        "version",
        "change_type",
        "project",
        "assigned_to",
        "geometry",
        "address_sub_thoroughfare",
        "address_thoroughfare",
        "address_suite",
        "address_locality",
        "address_sub_admin_area",
        "address_admin_area",
        "address_postal_code",
        "address_country",
        AddressFullColumn,
    };

    /// <summary>
    /// System columns still worth keeping. They are not answers, but throwing away who collected a
    /// record and when it was last touched would lose the only provenance the export carries.
    /// </summary>
    private static readonly string[] ExtraColumns =
    [
        "updated_at",
        "updated_by",
        "version",
        "project",
        "assigned_to",
        AddressFullColumn,
    ];

    public string SourceCode => MigrationSourceCodes.Fulcrum;

    public string DisplayNameEn => "Fulcrum export (Excel)";

    public string DisplayNameAr => "تصدير فولكرم (إكسل)";

    public IReadOnlyList<string> AcceptedExtensions => [".xlsx"];

    public MigrationParseResult Parse(Stream content, MigrationParseContext context, CancellationToken cancellationToken)
    {
        using var workbook = OpenWorkbook(content);

        // By index, never by name — see the class remarks.
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new MigrationParseException("The workbook has no worksheets.");

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        if (lastRow < 2 || lastColumn < 1)
        {
            throw new MigrationParseException("The first worksheet holds no data rows below its header.");
        }

        var headers = ReadHeaders(sheet, lastColumn);

        if (!headers.ContainsKey(ExternalIdColumn))
        {
            throw new MigrationParseException(
                $"The first worksheet has no '{ExternalIdColumn}' column, so its rows cannot be identified.");
        }

        var records = new List<MigrationRecord>(lastRow - 1);

        for (var row = 2; row <= lastRow; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            records.Add(ReadRecord(sheet, row, headers, context));
        }

        var columns = Classify(headers, context);

        return new MigrationParseResult
        {
            Records = records,
            UnmappedColumns = columns.Unmapped,
            IgnoredColumns = columns.Ignored,
            MappedColumnCount = columns.MappedCount,
            SourceColumnCount = headers.Count,
        };
    }

    private static IXLWorkbook OpenWorkbook(Stream content)
    {
        try
        {
            return new XLWorkbook(content);
        }
        catch (Exception exception) when (exception is not MigrationParseException)
        {
            throw new MigrationParseException($"The file could not be read as an Excel workbook: {exception.Message}");
        }
    }

    /// <summary>
    /// Column name to column number. A duplicated header keeps its first column — the answer a
    /// reader would see in Excel — rather than being overwritten by a later empty copy of itself.
    /// </summary>
    private static Dictionary<string, int> ReadHeaders(IXLWorksheet sheet, int lastColumn)
    {
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var column = 1; column <= lastColumn; column++)
        {
            var name = sheet.Cell(1, column).GetString().Trim();

            if (name.Length > 0)
            {
                headers.TryAdd(name, column);
            }
        }

        return headers;
    }

    private static MigrationRecord ReadRecord(
        IXLWorksheet sheet,
        int row,
        IReadOnlyDictionary<string, int> headers,
        MigrationParseContext context)
    {
        var externalId = CellText(sheet, row, headers, ExternalIdColumn) ?? string.Empty;

        if (externalId.Length == 0)
        {
            return new MigrationRecord
            {
                ExternalId = string.Empty,
                RowNumber = row,
                ParseError = $"Row {row} has no '{ExternalIdColumn}', so it cannot be identified or de-duplicated.",
            };
        }

        var latitude = CellDouble(sheet, row, headers, LatitudeColumn);
        var longitude = CellDouble(sheet, row, headers, LongitudeColumn);

        var answers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var media = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, column) in headers)
        {
            if (!IsAnswerColumn(name, context))
            {
                continue;
            }

            var text = CellText(sheet, row, column);
            if (text is null)
            {
                continue;
            }

            if (context.MediaDataNames.Contains(name))
            {
                var keys = SplitMediaKeys(text);
                if (keys.Count > 0)
                {
                    media[name] = keys;
                }

                continue;
            }

            answers[name] = text;
        }

        // Fulcrum's Address is nine columns plus the record's own point; ours is one field.
        if (context.KnownDataNames.Contains(GeolocationDataName) && latitude is not null && longitude is not null)
        {
            answers[GeolocationDataName] = SurveyGeolocation.ToJson(
                new SurveyGeolocationPoint(
                    latitude.Value,
                    longitude.Value,
                    CellText(sheet, row, headers, AddressFullColumn)));
        }

        var status = CellText(sheet, row, headers, StatusColumn);

        return new MigrationRecord
        {
            ExternalId = externalId,
            RowNumber = row,
            Latitude = latitude,
            Longitude = longitude,
            CapturedAt = CellTimestamp(sheet, row, headers, CreatedAtColumn),
            CapturedBy = CellText(sheet, row, headers, CreatedByColumn),
            SourceStatus = status,
            IsApproved = string.Equals(status, ApprovedStatus, StringComparison.OrdinalIgnoreCase),
            Answers = answers,
            Media = media,
            Extras = ReadExtras(sheet, row, headers),
        };
    }

    /// <summary>
    /// A column is an answer when the published template declares a field of that exact name. The
    /// caption/url sidecars and the system columns are filtered first so a template that happened to
    /// declare one of those names could still not turn it into an answer.
    /// </summary>
    private static bool IsAnswerColumn(string name, MigrationParseContext context) =>
        !IsIgnoredColumn(name) && context.KnownDataNames.Contains(name);

    /// <summary>
    /// Columns this adapter never reads as answers, whatever the template says: Fulcrum's own
    /// bookkeeping, and the <c>_caption</c> / <c>_url</c> sidecars that sit beside every media column.
    /// </summary>
    private static bool IsIgnoredColumn(string name) =>
        SystemColumns.Contains(name)
        || name.EndsWith(CaptionSuffix, StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(UrlSuffix, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> SplitMediaKeys(string value) =>
        value.Split(MediaKeySeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Dictionary<string, string?> ReadExtras(
        IXLWorksheet sheet,
        int row,
        IReadOnlyDictionary<string, int> headers)
    {
        var extras = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in ExtraColumns)
        {
            var value = CellText(sheet, row, headers, name);
            if (value is not null)
            {
                extras[name] = value;
            }
        }

        return extras;
    }

    /// <summary>
    /// Sorts every header into exactly one of three buckets, in one pass.
    ///
    /// One pass rather than three predicates on purpose: the page shows the three counts as a
    /// breakdown of the file's column count, and separately-derived numbers only add up by
    /// coincidence. Partitioning here makes "mapped + unmapped + ignored = total" true by
    /// construction.
    ///
    /// Ignored is decided first and without consulting the template — these are columns the adapter
    /// knows this source always carries and we never want, so a template that happened to declare a
    /// field called <c>version</c> still could not turn Fulcrum's revision counter into an answer.
    /// </summary>
    private static ColumnClassification Classify(
        IReadOnlyDictionary<string, int> headers,
        MigrationParseContext context)
    {
        var unmapped = new List<string>();
        var ignored = new List<string>();
        var mappedCount = 0;

        foreach (var name in headers.Keys)
        {
            if (IsIgnoredColumn(name))
            {
                ignored.Add(name);
            }
            else if (context.KnownDataNames.Contains(name))
            {
                mappedCount++;
            }
            else
            {
                unmapped.Add(name);
            }
        }

        unmapped.Sort(StringComparer.Ordinal);
        ignored.Sort(StringComparer.Ordinal);

        return new ColumnClassification(mappedCount, unmapped, ignored);
    }

    private sealed record ColumnClassification(
        int MappedCount,
        IReadOnlyList<string> Unmapped,
        IReadOnlyList<string> Ignored);

    private static string? CellText(
        IXLWorksheet sheet,
        int row,
        IReadOnlyDictionary<string, int> headers,
        string columnName) =>
        headers.TryGetValue(columnName, out var column) ? CellText(sheet, row, column) : null;

    private static string? CellText(IXLWorksheet sheet, int row, int column)
    {
        var cell = sheet.Cell(row, column);

        if (cell.IsEmpty())
        {
            return null;
        }

        // GetFormattedString rather than GetString: a numeric cell would otherwise come back in the
        // invariant "1.3E+11" form for the long meter and plate numbers this export carries.
        var text = cell.DataType == XLDataType.Number
            ? cell.GetDouble().ToString("0.############################", CultureInfo.InvariantCulture)
            : cell.GetFormattedString();

        text = text.Trim();
        return text.Length == 0 ? null : text;
    }

    private static double? CellDouble(
        IXLWorksheet sheet,
        int row,
        IReadOnlyDictionary<string, int> headers,
        string columnName)
    {
        var text = CellText(sheet, row, headers, columnName);

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// Reads a Fulcrum timestamp. Exported as text with a trailing <c>UTC</c> when the sheet was
    /// written by their SQL/CSV pipeline, and as a real date cell otherwise, so both are accepted.
    /// The value is UTC either way — an offset is never carried in the file.
    /// </summary>
    private static DateTimeOffset? CellTimestamp(
        IXLWorksheet sheet,
        int row,
        IReadOnlyDictionary<string, int> headers,
        string columnName)
    {
        if (!headers.TryGetValue(columnName, out var column))
        {
            return null;
        }

        var cell = sheet.Cell(row, column);

        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var dateCell))
        {
            return new DateTimeOffset(DateTime.SpecifyKind(dateCell, DateTimeKind.Utc));
        }

        var text = CellText(sheet, row, column);
        if (text is null)
        {
            return null;
        }

        text = text.Replace("UTC", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed)
            ? new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Utc))
            : null;
    }
}
