using KH.Domain.Entities.Fsms.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class FsmsTaskTypeConfiguration : IEntityTypeConfiguration<FsmsTaskType>
{
    public void Configure(EntityTypeBuilder<FsmsTaskType> builder)
    {
        builder.ToTable(FsmsTableNames.LookupTaskType);

        // Task type ids mirror the WFM source, so the PK is not database-generated.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(250).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(250).IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName($"IX_{FsmsTableNames.LookupTaskType}_Code");
    }
}
