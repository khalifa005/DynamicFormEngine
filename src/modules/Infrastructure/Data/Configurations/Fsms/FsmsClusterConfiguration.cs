using KH.Domain.Entities.Fsms.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class FsmsClusterConfiguration : IEntityTypeConfiguration<FsmsCluster>
{
    public void Configure(EntityTypeBuilder<FsmsCluster> builder)
    {
        builder.ToTable(FsmsTableNames.LookupCluster);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(250).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(250).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName($"IX_{FsmsTableNames.LookupCluster}_Code");
    }
}
