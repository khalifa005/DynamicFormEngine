using KH.Domain.Entities.Fsms.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class DataMigrationRunConfiguration : IEntityTypeConfiguration<DataMigrationRun>
{
    public void Configure(EntityTypeBuilder<DataMigrationRun> builder)
    {
        builder.ToTable(FsmsTableNames.DataMigrationRuns);

        builder.Property(x => x.SourceCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Mode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(400).IsRequired();
        builder.Property(x => x.StoredPath).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.RequestedBy).HasMaxLength(256);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);

        // A file can carry a hundred columns the chosen template does not know; the whole list is
        // what tells an operator they picked the wrong form, so it is not truncated to a column cap.
        builder.Property(x => x.UnmappedColumns).HasColumnType("NVARCHAR(MAX)");

        // Likewise unbounded: a Fulcrum export carries a caption and a url column beside every media
        // field, so this list is routinely longer than the mapped one.
        builder.Property(x => x.IgnoredColumns).HasColumnType("NVARCHAR(MAX)");

        // The operator's placement choices, read back as a unit by the job. No column is ever
        // filtered on, so a single JSON payload beats spreading it over columns that would need a
        // migration each time a future source needs to remember something new.
        builder.Property(x => x.OptionsJson).HasColumnType("NVARCHAR(MAX)");

        builder.HasMany(x => x.Records)
            .WithOne()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Records).UsePropertyAccessMode(PropertyAccessMode.Field);

        // The run grid orders newest first and filters on source and status.
        builder.HasIndex(x => new { x.SourceCode, x.Status })
            .HasDatabaseName($"IX_{FsmsTableNames.DataMigrationRuns}_SourceCode_Status");
    }
}
