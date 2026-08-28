using KH.Domain.Entities.Fsms.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class FsmsDepartmentConfiguration : IEntityTypeConfiguration<FsmsDepartment>
{
    public void Configure(EntityTypeBuilder<FsmsDepartment> builder)
    {
        builder.ToTable(FsmsTableNames.LookupDepartment);

        // Department ids mirror the WFM source, so the PK is not database-generated.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.NameEn).HasMaxLength(250).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(250).IsRequired();
    }
}
