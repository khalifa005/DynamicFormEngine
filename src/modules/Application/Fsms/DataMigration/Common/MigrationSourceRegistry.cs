namespace KH.Application.Fsms.DataMigration.Common;

/// <summary>
/// The registered source adapters, looked up by code. A thin wrapper over the DI collection so the
/// command, the job and the sources query all resolve a source the same way — and so registering a
/// new source stays a one-line DI change with nothing to update here.
/// </summary>
public interface IMigrationSourceRegistry
{
    IReadOnlyList<IMigrationSourceAdapter> All { get; }

    /// <summary>The adapter for <paramref name="sourceCode"/>, or null when none is registered.</summary>
    IMigrationSourceAdapter? Find(string? sourceCode);
}

public sealed class MigrationSourceRegistry(IEnumerable<IMigrationSourceAdapter> adapters) : IMigrationSourceRegistry
{
    private readonly IReadOnlyList<IMigrationSourceAdapter> _adapters = adapters.ToList();

    public IReadOnlyList<IMigrationSourceAdapter> All => _adapters;

    public IMigrationSourceAdapter? Find(string? sourceCode) =>
        string.IsNullOrWhiteSpace(sourceCode)
            ? null
            : _adapters.FirstOrDefault(adapter =>
                string.Equals(adapter.SourceCode, sourceCode.Trim(), StringComparison.OrdinalIgnoreCase));
}
