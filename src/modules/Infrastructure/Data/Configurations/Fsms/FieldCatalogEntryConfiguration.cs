using KH.Domain.Entities.Fsms.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class FieldCatalogEntryConfiguration : IEntityTypeConfiguration<FieldCatalogEntry>
{
    public void Configure(EntityTypeBuilder<FieldCatalogEntry> builder)
    {
        builder.ToTable(FsmsTableNames.FieldCatalog);

        builder.Property(x => x.DataName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.FieldType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.LabelEn).HasMaxLength(500);
        builder.Property(x => x.LabelAr).HasMaxLength(500);
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.HasIndex(x => x.DataName)
            .IsUnique()
            .HasDatabaseName($"IX_{FsmsTableNames.FieldCatalog}_DataName");
    }
}
