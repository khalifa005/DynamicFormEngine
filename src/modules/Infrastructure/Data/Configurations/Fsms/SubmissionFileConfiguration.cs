using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Submissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class SubmissionFileConfiguration : IEntityTypeConfiguration<SubmissionFile>
{
    public void Configure(EntityTypeBuilder<SubmissionFile> builder)
    {
        builder.ToTable(FsmsTableNames.SubmissionFiles);

        builder.Ignore(x => x.IsPending);

        builder.Property(x => x.DataName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(400).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RelativePath).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();

        // Rows written before staged media existed are all application-owned, which is exactly what
        // the default backfills them to.
        builder.Property(x => x.StorageKind)
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(SubmissionFileStorageKinds.Managed);

        builder.HasIndex(x => x.FileId)
            .IsUnique()
            .HasDatabaseName($"IX_{FsmsTableNames.SubmissionFiles}_FileId");

        // Submit-time linking looks files up by submission; the orphan sweep filters on status.
        builder.HasIndex(x => x.SubmissionId)
            .HasDatabaseName($"IX_{FsmsTableNames.SubmissionFiles}_SubmissionId");

        builder.HasIndex(x => new { x.Status, x.Created })
            .HasDatabaseName($"IX_{FsmsTableNames.SubmissionFiles}_Status_Created");
    }
}
