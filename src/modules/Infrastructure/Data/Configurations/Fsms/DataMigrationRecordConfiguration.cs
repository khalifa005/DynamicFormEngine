using KH.Domain.Entities.Fsms.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class DataMigrationRecordConfiguration : IEntityTypeConfiguration<DataMigrationRecord>
{
    public void Configure(EntityTypeBuilder<DataMigrationRecord> builder)
    {
        builder.ToTable(FsmsTableNames.DataMigrationRecords);

        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ExternalId).HasMaxLength(200);
        builder.Property(x => x.SurveyCode).HasMaxLength(60);
        builder.Property(x => x.Message).HasMaxLength(1000);

        // The detail table pages by run and orders by row, and filters to one outcome — in practice,
        // to the failures.
        builder.HasIndex(x => new { x.RunId, x.Status, x.RowNumber })
            .HasDatabaseName($"IX_{FsmsTableNames.DataMigrationRecords}_RunId_Status_RowNumber");

        // Answers "was this source record already imported, and as what?" without reading the run.
        builder.HasIndex(x => x.ExternalId)
            .HasDatabaseName($"IX_{FsmsTableNames.DataMigrationRecords}_ExternalId");
    }
}
