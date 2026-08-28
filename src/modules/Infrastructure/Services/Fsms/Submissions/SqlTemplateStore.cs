using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace KH.Infrastructure.Services.Fsms.Submissions;

/// <summary>
/// Loads the editable native-SQL templates for survey submissions from
/// <c>sql/survey-submissions.sql.json</c> (copied next to the app) and hot-reloads on file change.
/// Keeps SQL text out of C# so it can be tuned without a redeploy.
/// </summary>
public sealed class SqlTemplateStore : IDisposable
{
    private const string RelativePath = "sql/survey-submissions.sql.json";

    private readonly string _filePath;
    private readonly ILogger<SqlTemplateStore> _logger;
    private readonly FileSystemWatcher? _watcher;
    private volatile IReadOnlyDictionary<string, string> _statements = new ConcurrentDictionary<string, string>();

    public SqlTemplateStore(ILogger<SqlTemplateStore> logger)
    {
        _logger = logger;
        _filePath = Path.Combine(AppContext.BaseDirectory, RelativePath);

        Load();

        var directory = Path.GetDirectoryName(_filePath);
        if (directory is not null && Directory.Exists(directory))
        {
            _watcher = new FileSystemWatcher(directory, Path.GetFileName(_filePath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += (_, _) => SafeReload();
            _watcher.Created += (_, _) => SafeReload();
        }
    }

    /// <summary>Returns the SQL template for <paramref name="key"/>, or throws when missing.</summary>
    public string Get(string key)
    {
        if (_statements.TryGetValue(key, out var sql) && !string.IsNullOrWhiteSpace(sql))
        {
            return sql;
        }

        throw new InvalidOperationException($"SQL template '{key}' was not found in '{RelativePath}'.");
    }

    private void SafeReload()
    {
        try
        {
            // Editors often fire multiple change events; a small delay lets the write settle.
            Thread.Sleep(150);
            Load();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reload survey-submission SQL templates from {Path}.", _filePath);
        }
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogWarning("Survey-submission SQL template file not found at {Path}.", _filePath);
            return;
        }

        var json = File.ReadAllText(_filePath);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];

        var statements = new ConcurrentDictionary<string, string>(
            parsed.Where(kvp => !kvp.Key.StartsWith('_')),
            StringComparer.Ordinal);

        _statements = statements;
        _logger.LogInformation("Loaded {Count} survey-submission SQL templates from {Path}.", statements.Count, _filePath);
    }

    public void Dispose() => _watcher?.Dispose();
}
