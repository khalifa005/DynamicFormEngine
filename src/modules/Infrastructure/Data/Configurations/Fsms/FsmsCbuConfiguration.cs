using KH.Domain.Entities.Fsms.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class FsmsCbuConfiguration : IEntityTypeConfiguration<FsmsCbu>
{
    public void Configure(EntityTypeBuilder<FsmsCbu> builder)
    {
        builder.ToTable(FsmsTableNames.LookupCbu);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ClusterCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(250).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(250).IsRequired();
        builder.Property(x => x.OrgCode).HasMaxLength(50);
        builder.Property(x => x.DefaultTaskZone).HasMaxLength(50);

        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName($"IX_{FsmsTableNames.LookupCbu}_Code");

        // "Every CBU under this cluster" is the cascading dropdown's only query.
        builder.HasIndex(x => x.ClusterCode).HasDatabaseName($"IX_{FsmsTableNames.LookupCbu}_ClusterCode");
    }
}
