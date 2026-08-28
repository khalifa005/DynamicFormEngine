using KH.Domain.Entities.Fsms.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class FsmsFaTypeConfiguration : IEntityTypeConfiguration<FsmsFaType>
{
    public void Configure(EntityTypeBuilder<FsmsFaType> builder)
    {
        builder.ToTable(FsmsTableNames.LookupFaType);

        builder.Property(x => x.FaTypeCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(250).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(250).IsRequired();

        builder.HasIndex(x => x.FaTypeCode).HasDatabaseName($"IX_{FsmsTableNames.LookupFaType}_FaTypeCode");
    }
}
