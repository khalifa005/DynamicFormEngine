using KH.Domain.Entities.Fsms.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class FsmsBranchConfiguration : IEntityTypeConfiguration<FsmsBranch>
{
    public void Configure(EntityTypeBuilder<FsmsBranch> builder)
    {
        builder.ToTable(FsmsTableNames.LookupBranch);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CbuCode).HasMaxLength(50);
        builder.Property(x => x.TaskZone).HasMaxLength(50);
        builder.Property(x => x.BranchCode).HasMaxLength(50);
        builder.Property(x => x.NameEn).HasMaxLength(250).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(250).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName($"IX_{FsmsTableNames.LookupBranch}_Code");

        // "Every branch under this CBU" is the cascading dropdown's only query.
        builder.HasIndex(x => x.CbuCode).HasDatabaseName($"IX_{FsmsTableNames.LookupBranch}_CbuCode");
    }
}
