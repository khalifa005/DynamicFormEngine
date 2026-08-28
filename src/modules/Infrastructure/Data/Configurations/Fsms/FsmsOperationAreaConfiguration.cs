using KH.Domain.Entities.Fsms.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class FsmsOperationAreaConfiguration : IEntityTypeConfiguration<FsmsOperationArea>
{
    public void Configure(EntityTypeBuilder<FsmsOperationArea> builder)
    {
        builder.ToTable(FsmsTableNames.LookupOperationArea);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CbuCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.MainAreaCode).HasMaxLength(50);
        builder.Property(x => x.NameEn).HasMaxLength(250).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(250).IsRequired();

        // Unique per CBU rather than globally: upstream reuses short area codes across CBUs, so a
        // global unique index would reject legitimate reference data.
        builder.HasIndex(x => new { x.CbuCode, x.Code })
            .IsUnique()
            .HasDatabaseName($"IX_{FsmsTableNames.LookupOperationArea}_CbuCode_Code");
    }
}
