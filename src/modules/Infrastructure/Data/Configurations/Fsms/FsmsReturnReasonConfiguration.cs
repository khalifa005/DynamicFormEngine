using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class FsmsReturnReasonConfiguration : IEntityTypeConfiguration<FsmsReturnReason>
{
    public void Configure(EntityTypeBuilder<FsmsReturnReason> builder)
    {
        builder.ToTable(FsmsTableNames.LookupReturnReason);

        builder.Property(x => x.Code).HasMaxLength(SurveyReturnReasons.MaxCodeLength).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(250).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(250).IsRequired();

        // The code is the natural key: the survey stores it, and the seeder upserts on it.
        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName($"IX_{FsmsTableNames.LookupReturnReason}_Code");
    }
}
