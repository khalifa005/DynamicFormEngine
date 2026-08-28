using KH.Application.Common.Interfaces;
using KH.Application.Common.Options;
using KH.Application.Common.Security;
using KH.Application.Fsms.DataMigration.Common;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Common;

namespace KH.Application.Fsms.DataMigration.Queries.GetMigrationSetup;

/// <summary>
/// Everything the import page needs before an operator can start: which external systems this
/// deployment can read, and where the migration archive lives.
///
/// The archive path is returned rather than described because guessing at it is exactly how an
/// import ends up finding nothing. Whoever has to fill that folder is told the absolute path, and
/// whether it is there yet.
/// </summary>
[Authorize(Policy = FsmsPolicies.ImportData)]
public record GetMigrationSetupQuery : IRequest<Result<MigrationSetupDto>>;

public sealed record MigrationSetupDto
{
    public IReadOnlyList<MigrationSourceDto> Sources { get; init; } = [];

    /// <summary>Absolute path of the folder migrated media is read from.</summary>
    public string ArchivePath { get; init; } = string.Empty;

    /// <summary>False when that folder has not been created yet — no run can succeed until it is.</summary>
    public bool ArchiveExists { get; init; }
}

public sealed record MigrationSourceDto
{
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;

    /// <summary>Extensions the page's file picker should accept, dot-prefixed.</summary>
    public IReadOnlyList<string> AcceptedExtensions { get; init; } = [];
}

public sealed class GetMigrationSetupQueryHandler(
    IMigrationSourceRegistry sourceRegistry,
    IFileStorage fileStorage,
    IOptions<DataMigrationOptions> options)
    : IRequestHandler<GetMigrationSetupQuery, Result<MigrationSetupDto>>
{
    public Task<Result<MigrationSetupDto>> Handle(GetMigrationSetupQuery request, CancellationToken cancellationToken)
    {
        var archive = fileStorage.Describe(options.Value.ArchiveFolder);

        var setup = new MigrationSetupDto
        {
            Sources = sourceRegistry.All
                .Select(adapter => new MigrationSourceDto
                {
                    Code = adapter.SourceCode,
                    NameEn = adapter.DisplayNameEn,
                    NameAr = adapter.DisplayNameAr,
                    AcceptedExtensions = adapter.AcceptedExtensions,
                })
                .OrderBy(source => source.NameEn, StringComparer.Ordinal)
                .ToList(),
            ArchivePath = archive.FullPath,
            ArchiveExists = archive.Exists,
        };

        return Task.FromResult(Result<MigrationSetupDto>.Success(setup));
    }
}
