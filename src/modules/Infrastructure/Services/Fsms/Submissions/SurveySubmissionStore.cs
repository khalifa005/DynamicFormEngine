using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using KH.Application.Fsms.Common.Definition;
using KH.Application.Fsms.Submissions.Interfaces;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Templates;
using KH.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.Core.Common;
using Shared.Core.Exceptions;

namespace KH.Infrastructure.Services.Fsms.Submissions;

/// <summary>
/// Native-SQL survey submission store. Every template writes to the one shared
/// <c>SUBMISSIONS</c> table: fixed base columns plus one nullable column per <c>data_name</c>,
/// separated by <c>TemplateId</c>. A column's SQL type comes from the canonical
/// <c>FIELD_CATALOG</c> entry, so a <c>data_name</c> can never hold two different types.
/// Identifiers are whitelisted against the template's field set and wrapped with <c>[]</c>;
/// all values flow through parameters.
/// </summary>
public sealed partial class SurveySubmissionStore(ApplicationDbContext context, SqlTemplateStore sql) : ISurveySubmissionStore
{
    private const string TableName = FsmsTableNames.Submissions;

    private const string SubmittedStatus = "Submitted";

    /// <summary>Columns every submission row carries, whatever its template.</summary>
    private static readonly IReadOnlyList<string> BaseColumns = SubmissionColumns.All;

    /// <summary>
    /// Base columns introduced after the table first shipped. A table created by an older build
    /// lacks them, so reconciliation adds them the same way it adds a template's field columns —
    /// nullable, never altering existing rows.
    /// </summary>
    private static readonly (string Column, string SqlType)[] AddableBaseColumns =
    [
        (SubmissionColumns.SurveyId, "BIGINT"),
        (SubmissionColumns.AssignmentId, "BIGINT"),
        (SubmissionColumns.FilledByRole, "NVARCHAR(30)"),
        (SubmissionColumns.ClientSubmissionId, "UNIQUEIDENTIFIER"),
    ];

    /// <summary>Enforces the client key's uniqueness — see <c>CreateClientSubmissionIndex</c>.</summary>
    private const string ClientSubmissionIndexName = "UX_SUBMISSIONS_ClientSubmissionId";

    public async Task ReconcileTableAsync(SurveyTemplate template, CancellationToken cancellationToken)
    {
        // Resolved before the connection is opened — this runs an EF query of its own.
        var columns = await MapColumnsAsync(template.DefinitionJson, cancellationToken);

        await OpenAsync(cancellationToken);
        try
        {
            if (!await TableExistsAsync(cancellationToken))
            {
                await ExecuteAsync(sql.Get("CreateTable"), cancellationToken);
            }

            foreach (var (column, type) in AddableBaseColumns.Concat(columns))
            {
                if (await ColumnExistsAsync(column, cancellationToken))
                {
                    // A field type's column can grow between builds — geolocation gained an address,
                    // so its 100-character column no longer holds the answer. Widening here is what
                    // keeps a table created by an older build writable.
                    await WidenIfNarrowerAsync(column, type, cancellationToken);
                    continue;
                }

                var addSql = sql.Get("AddColumn")
                    .Replace("{column}", Quote(column))
                    .Replace("{type}", type);
                await ExecuteAsync(addSql, cancellationToken);
            }

            // Added after the column loop so the column it covers is guaranteed to exist, and after
            // an existence check because a table created by an older build never had it.
            if (!await IndexExistsAsync(ClientSubmissionIndexName, cancellationToken))
            {
                await ExecuteAsync(sql.Get("CreateClientSubmissionIndex"), cancellationToken);
            }
        }
        finally
        {
            await CloseAsync();
        }
    }

    public async Task<long> InsertAsync(SubmissionInsert submission, CancellationToken cancellationToken)
    {
        // Types, not just names: a value has to be handed to ADO as the CLR type its column was
        // created with, since the client speaks the form-builder's vocabulary ('yes' for a yes/no
        // field) and the column is a BIT.
        var fieldTypes = await LoadFieldTypesAsync(submission.TemplateId, cancellationToken);

        // Case-insensitive dedupe: SQL Server column names are case-insensitive, so two answer
        // keys differing only in case would produce the same column twice in the INSERT.
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var accepted = submission.Answers
            // Trimmed to match the column even though the submit slices normalize keys first: this
            // is a public store, and a caller that skipped that step would otherwise have its
            // answers dropped in silence rather than written.
            .Select(kvp => (Key: kvp.Key.Trim(), kvp.Value))
            .Where(kvp => fieldTypes.ContainsKey(kvp.Key) && IsValidIdentifier(kvp.Key) && written.Add(kvp.Key))
            .ToList();

        var columnsBuilder = new StringBuilder();
        var paramsBuilder = new StringBuilder();

        await OpenAsync(cancellationToken);
        try
        {
            using var command = CreateCommand();

            AddParameter(command, "@templateId", submission.TemplateId);
            AddParameter(command, "@versionNo", (object?)submission.VersionNo);
            AddParameter(command, "@status", SubmittedStatus);
            AddParameter(command, "@surveyId", (object?)submission.SurveyId);
            AddParameter(command, "@assignmentId", (object?)submission.AssignmentId);
            AddParameter(command, "@filledByRole", (object?)submission.FilledByRole);
            AddParameter(command, "@submittedBy", (object?)submission.SubmittedBy);
            // The caller's own fill time wins when it has one — an importer of historical records
            // knows when the work was done, and the clock now would erase that.
            AddParameter(command, "@submittedDate", submission.SubmittedDate ?? DateTimeOffset.UtcNow);
            AddParameter(command, "@clientSubmissionId", (object?)submission.ClientSubmissionId);

            var index = 0;
            foreach (var (key, value) in accepted)
            {
                var paramName = "@p" + index.ToString(CultureInfo.InvariantCulture);
                columnsBuilder.Append(", ").Append(Quote(key));
                paramsBuilder.Append(", ").Append(paramName);
                AddParameter(command, paramName, CoerceForColumn(fieldTypes[key], key, value));
                index++;
            }

            command.CommandText = sql.Get("Insert")
                .Replace("{columns}", columnsBuilder.ToString())
                .Replace("{params}", paramsBuilder.ToString());

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }
        finally
        {
            await CloseAsync();
        }
    }

    public async Task UpdateAnswersAsync(
        long templateId,
        long submissionId,
        IReadOnlyDictionary<string, object?> answers,
        CancellationToken cancellationToken)
    {
        // The same gate as InsertAsync, and for the same reasons: a key the template does not declare
        // has no column, and one that is not a valid identifier must never reach the statement text.
        var fieldTypes = await LoadFieldTypesAsync(templateId, cancellationToken);

        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var accepted = answers
            .Select(kvp => (Key: kvp.Key.Trim(), kvp.Value))
            .Where(kvp => fieldTypes.ContainsKey(kvp.Key) && IsValidIdentifier(kvp.Key) && written.Add(kvp.Key))
            .ToList();

        // No accepted key means no SET clause, and `UPDATE ... SET WHERE` is a syntax error. Nothing
        // to write is not a failure, so return rather than build an invalid statement.
        if (accepted.Count == 0)
        {
            return;
        }

        var assignments = new StringBuilder();

        await OpenAsync(cancellationToken);
        try
        {
            using var command = CreateCommand();

            AddParameter(command, "@submissionId", submissionId);
            AddParameter(command, "@templateId", templateId);

            var index = 0;
            foreach (var (key, value) in accepted)
            {
                var paramName = "@p" + index.ToString(CultureInfo.InvariantCulture);

                if (index > 0)
                {
                    assignments.Append(", ");
                }

                assignments.Append(Quote(key)).Append(" = ").Append(paramName);
                AddParameter(command, paramName, CoerceForColumn(fieldTypes[key], key, value));
                index++;
            }

            command.CommandText = sql.Get("UpdateAnswers").Replace("{assignments}", assignments.ToString());

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await CloseAsync();
        }
    }

    public async Task<IReadOnlyDictionary<string, object?>?> GetByIdAsync(long templateId, long submissionId, CancellationToken cancellationToken)
    {
        var allowed = await LoadColumnWhitelistAsync(templateId, cancellationToken);

        await OpenAsync(cancellationToken);
        try
        {
            if (!await TableExistsAsync(cancellationToken))
            {
                return null;
            }

            var selectList = await BuildSelectListAsync(allowed, cancellationToken);

            using var command = CreateCommand();
            command.CommandText = sql.Get("GetById").Replace("{select}", selectList);
            AddParameter(command, "@submissionId", submissionId);
            AddParameter(command, "@templateId", templateId);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return ReadRow(reader);
        }
        finally
        {
            await CloseAsync();
        }
    }

    public async Task<IReadOnlyDictionary<string, object?>?> GetLatestBySurveyAsync(long templateId, long surveyId, CancellationToken cancellationToken)
    {
        var allowed = await LoadColumnWhitelistAsync(templateId, cancellationToken);

        await OpenAsync(cancellationToken);
        try
        {
            if (!await TableExistsAsync(cancellationToken))
            {
                return null;
            }

            var selectList = await BuildSelectListAsync(allowed, cancellationToken);

            using var command = CreateCommand();
            command.CommandText = sql.Get("GetLatestBySurvey").Replace("{select}", selectList);
            AddParameter(command, "@surveyId", surveyId);
            AddParameter(command, "@templateId", templateId);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return ReadRow(reader);
        }
        finally
        {
            await CloseAsync();
        }
    }

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ListBySurveyAsync(long templateId, long surveyId, CancellationToken cancellationToken)
    {
        var allowed = await LoadColumnWhitelistAsync(templateId, cancellationToken);

        await OpenAsync(cancellationToken);
        try
        {
            if (!await TableExistsAsync(cancellationToken))
            {
                return [];
            }

            var selectList = await BuildSelectListAsync(allowed, cancellationToken);

            using var command = CreateCommand();
            command.CommandText = sql.Get("ListBySurvey").Replace("{select}", selectList);
            AddParameter(command, "@surveyId", surveyId);
            AddParameter(command, "@templateId", templateId);

            var rows = new List<IReadOnlyDictionary<string, object?>>();

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(ReadRow(reader));
            }

            return rows;
        }
        finally
        {
            await CloseAsync();
        }
    }

    public async Task<(IReadOnlyList<IReadOnlyDictionary<string, object?>> Items, int Total)> ListAsync(long templateId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var allowed = await LoadColumnWhitelistAsync(templateId, cancellationToken);
        var safePage = page < 1 ? 1 : page;
        var safeSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        await OpenAsync(cancellationToken);
        try
        {
            if (!await TableExistsAsync(cancellationToken))
            {
                return ([], 0);
            }

            int total;
            using (var countCommand = CreateCommand())
            {
                countCommand.CommandText = sql.Get("CountByTemplate");
                AddParameter(countCommand, "@templateId", templateId);
                total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
            }

            var selectList = await BuildSelectListAsync(allowed, cancellationToken);

            var items = new List<IReadOnlyDictionary<string, object?>>();
            using (var listCommand = CreateCommand())
            {
                listCommand.CommandText = sql.Get("ListByTemplate").Replace("{select}", selectList);
                AddParameter(listCommand, "@templateId", templateId);
                AddParameter(listCommand, "@skip", (safePage - 1) * safeSize);
                AddParameter(listCommand, "@take", safeSize);

                using var reader = await listCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    items.Add(ReadRow(reader));
                }
            }

            return (items, total);
        }
        finally
        {
            await CloseAsync();
        }
    }

    public async Task<long?> FindByClientIdAsync(long templateId, Guid clientSubmissionId, CancellationToken cancellationToken)
    {
        await OpenAsync(cancellationToken);
        try
        {
            // A database that has never taken a submission has no table to search, which is a
            // "not seen before" answer rather than a failure.
            if (!await TableExistsAsync(cancellationToken)
                || !await ColumnExistsAsync(SubmissionColumns.ClientSubmissionId, cancellationToken))
            {
                return null;
            }

            using var command = CreateCommand();
            command.CommandText = sql.Get("GetIdByClientId");
            AddParameter(command, "@templateId", templateId);
            AddParameter(command, "@clientSubmissionId", clientSubmissionId);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is null or DBNull ? null : Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }
        finally
        {
            await CloseAsync();
        }
    }

    public async Task<IReadOnlyDictionary<Guid, long>> FindByClientIdsAsync(
        long templateId,
        IReadOnlyCollection<Guid> clientSubmissionIds,
        CancellationToken cancellationToken)
    {
        var found = new Dictionary<Guid, long>();

        if (clientSubmissionIds.Count == 0)
        {
            return found;
        }

        await OpenAsync(cancellationToken);
        try
        {
            if (!await TableExistsAsync(cancellationToken)
                || !await ColumnExistsAsync(SubmissionColumns.ClientSubmissionId, cancellationToken))
            {
                return found;
            }

            using var command = CreateCommand();
            AddParameter(command, "@templateId", templateId);

            // One parameter per id — the IN list is built from parameter names only, never from the
            // values themselves.
            var placeholders = new StringBuilder();
            var index = 0;
            foreach (var clientSubmissionId in clientSubmissionIds)
            {
                var paramName = "@c" + index.ToString(CultureInfo.InvariantCulture);
                if (index > 0)
                {
                    placeholders.Append(", ");
                }

                placeholders.Append(paramName);
                AddParameter(command, paramName, clientSubmissionId);
                index++;
            }

            command.CommandText = sql.Get("GetIdsByClientIds").Replace("{params}", placeholders.ToString());

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                found[reader.GetGuid(1)] = reader.GetInt64(0);
            }

            return found;
        }
        finally
        {
            await CloseAsync();
        }
    }

    /// <summary>
    /// Base columns plus the template's own <c>data_name</c> columns that physically exist.
    /// Projecting keeps a row payload to this template's fields instead of every column the
    /// shared table has accumulated for other templates.
    /// </summary>
    private async Task<string> BuildSelectListAsync(IReadOnlySet<string> allowed, CancellationToken cancellationToken)
    {
        var existing = await LoadPhysicalColumnsAsync(cancellationToken);

        var selected = new List<string>(BaseColumns.Count + allowed.Count);
        selected.AddRange(BaseColumns.Where(existing.Contains).Select(Quote));
        selected.AddRange(allowed
            .Where(column => existing.Contains(column) && !SubmissionColumns.IsBase(column))
            .Select(Quote));

        return selected.Count == 0 ? "*" : string.Join(", ", selected);
    }

    private async Task<HashSet<string>> LoadPhysicalColumnsAsync(CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var command = CreateCommand();
        command.CommandText = sql.Get("ColumnNames");
        AddParameter(command, "@tableName", TableName);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private async Task<HashSet<string>> LoadColumnWhitelistAsync(long templateId, CancellationToken cancellationToken)
    {
        var definitionJson = await context.SurveyTemplates
            .Where(t => t.Id == templateId)
            .Select(t => t.DefinitionJson)
            .FirstOrDefaultAsync(cancellationToken);

        var definition = SurveyDefinitionParser.Parse(definitionJson);
        return WritableFields(definition)
            .Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every name this template can write, with the field type its column is built from: one per
    /// field, plus a <c>&lt;data_name&gt;_other</c> companion for each choice field offering "Other"
    /// (see <see cref="SurveyChoiceOther"/>), which holds free text and so is typed as <c>text</c>.
    /// Read and write paths both go through here so a companion can never exist on one and not the
    /// other. Names that could not be a SQL identifier are dropped — publish rejects them, and an
    /// older template that slipped one through must not reach a statement.
    /// </summary>
    private static IEnumerable<(string Name, string FieldType)> WritableFields(SurveyDefinition definition)
    {
        foreach (var field in definition.Fields)
        {
            if (!IsValidIdentifier(field.DataName))
            {
                continue;
            }

            yield return (field.DataName, field.FieldType);

            if (!SurveyChoiceOther.NeedsCompanion(field))
            {
                continue;
            }

            var companion = SurveyChoiceOther.KeyFor(field.DataName);
            if (IsValidIdentifier(companion))
            {
                yield return (companion, BuilderElementTypes.Text);
            }
        }
    }

    /// <summary>
    /// The template's writable <c>data_name</c>s with the field type each column was created from.
    /// Read paths only need the names (<see cref="LoadColumnWhitelistAsync"/>); a write needs the
    /// types too, so a value can be converted to what its column holds.
    /// </summary>
    private async Task<Dictionary<string, string>> LoadFieldTypesAsync(long templateId, CancellationToken cancellationToken)
    {
        var definitionJson = await context.SurveyTemplates
            .Where(t => t.Id == templateId)
            .Select(t => t.DefinitionJson)
            .FirstOrDefaultAsync(cancellationToken);

        return await ResolveFieldTypesAsync(definitionJson, cancellationToken);
    }

    /// <summary>
    /// Resolves each field to the type its column is built on: the canonical <c>FIELD_CATALOG</c>
    /// entry where the <c>data_name</c> is registered, the template's own type otherwise. Insert and
    /// reconciliation share this so a value is never coerced to a type the column was not created
    /// with.
    /// </summary>
    private async Task<Dictionary<string, string>> ResolveFieldTypesAsync(string? definitionJson, CancellationToken cancellationToken)
    {
        var definition = SurveyDefinitionParser.Parse(definitionJson);

        var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, fieldType) in WritableFields(definition))
        {
            types.TryAdd(name, fieldType);
        }

        if (types.Count == 0)
        {
            return types;
        }

        var names = types.Keys.ToList();
        var canonical = await context.FieldCatalog
            .Where(c => names.Contains(c.DataName))
            .Select(c => new { c.DataName, c.FieldType })
            .ToListAsync(cancellationToken);

        foreach (var entry in canonical)
        {
            if (types.ContainsKey(entry.DataName))
            {
                types[entry.DataName] = entry.FieldType;
            }
        }

        return types;
    }

    private async Task<bool> TableExistsAsync(CancellationToken cancellationToken)
    {
        using var command = CreateCommand();
        command.CommandText = sql.Get("TableExists");
        AddParameter(command, "@tableName", TableName);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture) == 1;
    }

    private async Task<bool> ColumnExistsAsync(string columnName, CancellationToken cancellationToken)
    {
        using var command = CreateCommand();
        command.CommandText = sql.Get("ColumnExists");
        AddParameter(command, "@tableName", TableName);
        AddParameter(command, "@columnName", columnName);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture) == 1;
    }

    private async Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken)
    {
        using var command = CreateCommand();
        command.CommandText = sql.Get("IndexExists");
        AddParameter(command, "@tableName", TableName);
        AddParameter(command, "@indexName", indexName);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture) == 1;
    }

    private async Task ExecuteAsync(string commandText, CancellationToken cancellationToken)
    {
        using var command = CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Every statement this store issues goes through here so it joins whatever transaction the
    /// caller has open. ADO commands built straight off the connection do not enlist by themselves,
    /// and SQL Server rejects one issued on a connection with an active transaction it is not part
    /// of — which is what a bulk submit, writing here and through EF in one unit of work, needs.
    /// </summary>
    private DbCommand CreateCommand()
    {
        var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
        return command;
    }

    private Task OpenAsync(CancellationToken cancellationToken) => context.Database.OpenConnectionAsync(cancellationToken);

    private Task CloseAsync() => context.Database.CloseConnectionAsync();

    private static IReadOnlyDictionary<string, object?> ReadRow(DbDataReader reader)
    {
        var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.Ordinal);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var value = reader.GetValue(i);
            row[reader.GetName(i)] = value is DBNull ? null : value;
        }

        return row;
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    /// <summary>Converts JSON-sourced values (JsonElement) to CLR types SQL Server can bind.</summary>
    private static object? NormalizeValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonElement json)
        {
            return json.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => json.TryGetInt64(out var l) ? l : json.GetDouble(),
                JsonValueKind.String => json.GetString(),
                JsonValueKind.Array or JsonValueKind.Object => json.GetRawText(),
                _ => json.ToString(),
            };
        }

        return value;
    }

    /// <summary>
    /// Converts an answer to what its column holds. Values arrive in the form-builder's own
    /// vocabulary — a yes/no field stores the string <c>"yes"</c> so its rules can match on it —
    /// while the column is a <c>BIT</c>, so handing the raw value to ADO leaves SQL Server to fail
    /// the conversion with a message that names neither the field nor the answer. Converting here,
    /// and rejecting what cannot convert, is what turns that into an error the caller can act on.
    /// </summary>
    private static object? CoerceForColumn(string fieldType, string dataName, object? value)
    {
        var normalized = NormalizeValue(value);

        if (normalized is null)
        {
            return null;
        }

        return fieldType switch
        {
            BuilderElementTypes.YesNo => ToBoolean(dataName, normalized),
            BuilderElementTypes.Numeric => ToNumber(dataName, normalized),
            BuilderElementTypes.Date => ToDate(dataName, normalized),
            BuilderElementTypes.Time => ToTime(dataName, normalized),
            BuilderElementTypes.DateTime => ToDateTime(dataName, normalized),
            BuilderElementTypes.Geolocation => ToGeolocation(dataName, normalized),
            _ => normalized,
        };
    }

    /// <summary>
    /// Normalises a point to the canonical <c>{"lat":…,"lng":…,"address":…}</c> the column holds.
    /// Clients post it in several shapes — the object, that object's JSON text, a <c>"lat,lng"</c>
    /// pair — and an address of any length; normalising here is what keeps one shape in the column
    /// and what stops an oversized address reaching SQL Server as a truncation error naming nothing.
    /// </summary>
    private static object? ToGeolocation(string dataName, object value)
    {
        if (value is string text && string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!SurveyGeolocation.TryRead(value, out var point))
        {
            throw AnswerRejected(dataName, value, AnswerTypeNames.Geolocation);
        }

        return SurveyGeolocation.ToJson(point);
    }

    private static object? ToBoolean(string dataName, object value) => value switch
    {
        bool flag => flag,
        long number => number != 0,
        double number => number != 0,
        string text => ParseBoolean(dataName, text),
        _ => throw AnswerRejected(dataName, value, AnswerTypeNames.YesNo),
    };

    private static object? ParseBoolean(string dataName, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Trim().ToLowerInvariant() switch
        {
            BooleanTokens.Yes or BooleanTokens.True or BooleanTokens.One => true,
            BooleanTokens.No or BooleanTokens.False or BooleanTokens.Zero => false,
            _ => throw AnswerRejected(dataName, text, AnswerTypeNames.YesNo),
        };
    }

    private static object? ToNumber(string dataName, object value)
    {
        switch (value)
        {
            case long or double or decimal or int:
                return value;

            case string text when string.IsNullOrWhiteSpace(text):
                return null;

            case string text when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed):
                return parsed;

            default:
                throw AnswerRejected(dataName, value, AnswerTypeNames.Number);
        }
    }

    private static object? ToDate(string dataName, object value)
    {
        switch (value)
        {
            case DateTime date:
                return date.Date;

            case DateTimeOffset date:
                return date.Date;

            case string text when string.IsNullOrWhiteSpace(text):
                return null;

            case string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed):
                return parsed.Date;

            default:
                throw AnswerRejected(dataName, value, AnswerTypeNames.Date);
        }
    }

    /// <summary>
    /// Reads the local <c>YYYY-MM-DD HH:mm</c> the web client writes. The kind is left
    /// unspecified rather than converted: the answer is the wall clock the crew read on site,
    /// and shifting it by the server's offset would store a time nobody recorded.
    /// </summary>
    private static object? ToDateTime(string dataName, object value)
    {
        switch (value)
        {
            case DateTime moment:
                return moment;

            case DateTimeOffset moment:
                return moment.DateTime;

            case string text when string.IsNullOrWhiteSpace(text):
                return null;

            case string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed):
                return parsed;

            default:
                throw AnswerRejected(dataName, value, AnswerTypeNames.DateTime);
        }
    }

    private static object? ToTime(string dataName, object value)
    {
        switch (value)
        {
            case TimeSpan time:
                return time;

            case DateTime time:
                return time.TimeOfDay;

            case string text when string.IsNullOrWhiteSpace(text):
                return null;

            case string text when TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var parsed):
                return parsed;

            default:
                throw AnswerRejected(dataName, value, AnswerTypeNames.Time);
        }
    }

    /// <summary>
    /// A rejected answer is the caller's mistake, not a server fault: a <see cref="DomainException"/>
    /// answers a single submit with 400 and the field's name, and lets a bulk run fail that one item
    /// while the rest of the crew's queue goes through.
    /// </summary>
    private static DomainException AnswerRejected(string dataName, object value, string expected) =>
        new($"The answer for '{dataName}' is not a valid {expected} value: '{value}'.", ApiErrorCodes.ValidationError);

    /// <summary>What a yes/no answer may arrive as — the builder stores 'yes'/'no'.</summary>
    private static class BooleanTokens
    {
        public const string Yes = "yes";
        public const string No = "no";
        public const string True = "true";
        public const string False = "false";
        public const string One = "1";
        public const string Zero = "0";
    }

    /// <summary>How a rejected answer's expected type reads in the error message.</summary>
    private static class AnswerTypeNames
    {
        public const string YesNo = "yes/no";
        public const string Number = "numeric";
        public const string Date = "date";
        public const string Time = "time";
        public const string DateTime = "date & time";
        public const string Geolocation = "geolocation";
    }

    /// <summary>
    /// Maps the template's fields to (column, SQL type). The type is taken from the canonical
    /// <c>FIELD_CATALOG</c> entry so every template sharing a <c>data_name</c> shares its column
    /// type; the field's own type is only a fallback for a name not yet in the catalog.
    /// </summary>
    private async Task<IReadOnlyList<(string Column, string SqlType)>> MapColumnsAsync(string? definitionJson, CancellationToken cancellationToken)
    {
        var fieldTypes = await ResolveFieldTypesAsync(definitionJson, cancellationToken);

        return fieldTypes
            .Select(field => (field.Key, SqlTypeFor(field.Value)))
            .ToList();
    }

    /// <summary>
    /// Holds the canonical <c>{"lat":…,"lng":…,"address":…}</c> answer. The coordinates take ~50
    /// characters and <see cref="SurveyGeolocation.MaxAddressLength"/> caps the address, so the value
    /// always fits with room for the JSON envelope and an Arabic address.
    /// </summary>
    private const string GeolocationColumnType = "NVARCHAR(500)";

    private static string SqlTypeFor(string fieldType) => fieldType switch
    {
        BuilderElementTypes.Numeric => "DECIMAL(18,4)",
        BuilderElementTypes.YesNo => "BIT",
        BuilderElementTypes.Date => "DATE",
        BuilderElementTypes.Time => "TIME",
        // No offset: the answer is the wall clock the crew read on site, not an instant.
        BuilderElementTypes.DateTime => "DATETIME2(0)",
        BuilderElementTypes.SingleChoice => "NVARCHAR(400)",
        BuilderElementTypes.Geolocation => GeolocationColumnType,
        // A decoded code is short — an asset tag or a serial, not a QR carrying a document.
        BuilderElementTypes.Barcode => "NVARCHAR(400)",
        _ => "NVARCHAR(MAX)",
    };

    /// <summary>
    /// Grows <paramref name="column"/> to <paramref name="sqlType"/> when it is a shorter
    /// <c>NVARCHAR</c> than the field now needs, and does nothing otherwise. Only widening is ever
    /// issued: it preserves every existing row, so this can run on each reconciliation — which is
    /// every publish, every survey creation and every submit — without a guard of its own. Any
    /// non-<c>NVARCHAR(n)</c> target, and any column already at <c>NVARCHAR(MAX)</c>, is left alone;
    /// changing those is a data migration, not something the write path should decide.
    /// </summary>
    private async Task WidenIfNarrowerAsync(string column, string sqlType, CancellationToken cancellationToken)
    {
        var match = NVarCharLengthRegex().Match(sqlType);
        if (!match.Success)
        {
            return;
        }

        var wanted = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

        var current = await ColumnMaxLengthAsync(column, cancellationToken);
        // -1 is NVARCHAR(MAX) — already wider than any fixed length. Bytes, so two per character.
        if (current is null or MaxLengthSentinel || current / 2 >= wanted)
        {
            return;
        }

        var alterSql = sql.Get("AlterColumn")
            .Replace("{column}", Quote(column))
            .Replace("{type}", sqlType);
        await ExecuteAsync(alterSql, cancellationToken);
    }

    /// <summary>What <c>sys.columns.max_length</c> reports for an unbounded column.</summary>
    private const int MaxLengthSentinel = -1;

    private async Task<int?> ColumnMaxLengthAsync(string columnName, CancellationToken cancellationToken)
    {
        using var command = CreateCommand();
        command.CommandText = sql.Get("ColumnMaxLength");
        AddParameter(command, "@tableName", TableName);
        AddParameter(command, "@columnName", columnName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Defers to <see cref="SurveyDataName"/> so the names publish accepts are exactly the names
    /// that can be written — a second copy of the rule here is how one drifts from the other.
    /// </summary>
    private static bool IsValidIdentifier(string? identifier) => SurveyDataName.IsValid(identifier);

    private static string Quote(string identifier)
    {
        if (!IsValidIdentifier(identifier))
        {
            throw new InvalidOperationException($"Rejected unsafe SQL identifier '{identifier}'.");
        }

        return "[" + identifier.Replace("]", "]]") + "]";
    }

    /// <summary>Matches a bounded <c>NVARCHAR(n)</c> only — <c>NVARCHAR(MAX)</c> deliberately does not.</summary>
    [GeneratedRegex(@"^NVARCHAR\((\d+)\)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex NVarCharLengthRegex();
}
