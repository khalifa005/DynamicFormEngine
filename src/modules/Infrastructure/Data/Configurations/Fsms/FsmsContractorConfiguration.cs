using KH.Domain.Entities.Fsms.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class FsmsContractorConfiguration : IEntityTypeConfiguration<FsmsContractor>
{
    public void Configure(EntityTypeBuilder<FsmsContractor> builder)
    {
        builder.ToTable(FsmsTableNames.LookupContractor);

        builder.Property(x => x.PoNumber).HasMaxLength(FsmsContractor.PoNumberMaxLength).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(FsmsContractor.NameMaxLength).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(FsmsContractor.NameMaxLength).IsRequired();
        builder.Property(x => x.CommercialRegistration).HasMaxLength(FsmsContractor.CommercialRegistrationMaxLength);

        builder.HasIndex(x => x.PoNumber)
            .IsUnique()
            .HasDatabaseName($"IX_{FsmsTableNames.LookupContractor}_PoNumber");
    }
}
