using KH.Application.Fsms.Common.Definition;

namespace KH.Application.Fsms.DataMigration.Common;

/// <summary>
/// Reads one external system's export into <see cref="MigrationRecord"/>s. This is the only place
/// a source's file layout, column names and status vocabulary are allowed to live — everything
/// downstream (the command, the job, the page) works in the canonical record, so adding Microsoft
/// Forms later means writing one of these and nothing else.
///
/// Registered per source and selected by <see cref="SourceCode"/>.
/// </summary>
public interface IMigrationSourceAdapter
{
    /// <summary>See <c>MigrationSourceCodes</c>.</summary>
    string SourceCode { get; }

    string DisplayNameEn { get; }
    string DisplayNameAr { get; }

    /// <summary>File extensions this adapter can read, dot-prefixed and lower-case.</summary>
    IReadOnlyList<string> AcceptedExtensions { get; }

    /// <summary>
    /// Reads the whole export. Rows that cannot be understood come back as records carrying a
    /// <see cref="MigrationRecord.ParseError"/> rather than being dropped, so the run reports them
    /// instead of silently importing fewer records than the file holds.
    /// </summary>
    /// <exception cref="MigrationParseException">The file as a whole could not be read.</exception>
    MigrationParseResult Parse(Stream content, MigrationParseContext context, CancellationToken cancellationToken);
}

/// <summary>
/// What the template says about the fields being imported into. Handed to the adapter so which
/// columns are answers, and which of those hold media, is decided by the published form rather than
/// by a list copied into each adapter and left to drift when the template changes.
/// </summary>
public sealed class MigrationParseContext
{
    public MigrationParseContext(SurveyDefinition definition)
    {
        Definition = definition;

        KnownDataNames = definition.Fields
            .Select(field => field.DataName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        MediaDataNames = definition.Fields
            .Where(field => BuilderElementTypes.IsMedia(field.FieldType))
            .Select(field => field.DataName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public SurveyDefinition Definition { get; }

    /// <summary>Every leaf <c>data_name</c> the published template declares.</summary>
    public IReadOnlySet<string> KnownDataNames { get; }

    /// <summary>The subset whose answers are file references rather than values.</summary>
    public IReadOnlySet<string> MediaDataNames { get; }
}

/// <summary>Everything one pass over the source file produced.</summary>
public sealed record MigrationParseResult
{
    public IReadOnlyList<MigrationRecord> Records { get; init; } = [];

    /// <summary>
    /// Columns in the file that no field of the chosen template claims. Reported rather than treated
    /// as an error: an export routinely carries system columns and per-media sidecar columns we do
    /// not model, and the operator is the one who can tell an expected extra from a broken mapping.
    ///
    /// Now that the operator picks the template, this is also the primary signal that they picked
    /// the wrong one — a file whose columns match almost nothing is exactly what that looks like.
    /// </summary>
    public IReadOnlyList<string> UnmappedColumns { get; init; } = [];

    /// <summary>
    /// Columns the adapter passed over on purpose — the source's own bookkeeping and the sidecar
    /// columns beside each media field. Named, not merely counted: "43 ignored" asks the operator to
    /// trust that all forty-three were noise, and the only way to answer that is to read them. A
    /// column that should have been an answer hides in this list, not in <see cref="UnmappedColumns"/>,
    /// because the adapter filters its own exclusions before the template is ever consulted.
    /// </summary>
    public IReadOnlyList<string> IgnoredColumns { get; init; } = [];

    /// <summary>How many of the file's columns did land on a field of the chosen template.</summary>
    public int MappedColumnCount { get; init; }

    /// <summary>
    /// Every column in the file. Reported so "37 matched" can be read as a proportion — 37 of 40 is
    /// a healthy import, 37 of 400 is the wrong template, and the number alone cannot tell them apart.
    /// </summary>
    public int SourceColumnCount { get; init; }
}

/// <summary>The source file could not be read at all — wrong format, missing sheet, no header row.</summary>
public sealed class MigrationParseException(string message) : Exception(message);
