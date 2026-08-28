using KH.Domain.Entities.Fsms.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class FsmsCustomerTypeConfiguration : IEntityTypeConfiguration<FsmsCustomerType>
{
    public void Configure(EntityTypeBuilder<FsmsCustomerType> builder)
    {
        builder.ToTable(FsmsTableNames.LookupCustomerType);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(250).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(250).IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName($"IX_{FsmsTableNames.LookupCustomerType}_Code");
    }
}
