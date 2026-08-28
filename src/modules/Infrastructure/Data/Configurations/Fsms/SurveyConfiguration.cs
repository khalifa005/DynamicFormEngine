using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Surveys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class SurveyConfiguration : IEntityTypeConfiguration<Survey>
{
    public void Configure(EntityTypeBuilder<Survey> builder)
    {
        builder.ToTable(FsmsTableNames.Surveys);

        builder.Property(x => x.SurveyCode).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.FaId).HasMaxLength(100);
        builder.Property(x => x.TaskCode).HasMaxLength(100);
        builder.Property(x => x.FaTypeCode).HasMaxLength(50);
        builder.Property(x => x.CustomerName).HasMaxLength(SurveyFieldLimits.CustomerName);
        builder.Property(x => x.CustomerPhone).HasMaxLength(SurveyFieldLimits.CustomerPhone);
        builder.Property(x => x.MeterNumber).HasMaxLength(SurveyFieldLimits.MeterNumber);
        builder.Property(x => x.Hcn).HasMaxLength(SurveyFieldLimits.Hcn);
        builder.Property(x => x.SourceComment).HasMaxLength(SurveyFieldLimits.SourceComment);
        builder.Property(x => x.IsExternalTask).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CbuCode).HasMaxLength(50);
        builder.Property(x => x.BranchCode).HasMaxLength(50);
        builder.Property(x => x.OperationAreaCode).HasMaxLength(50);
        builder.Property(x => x.AssignedBy).HasMaxLength(256);
        builder.Property(x => x.ReceivedBy).HasMaxLength(256);
        builder.Property(x => x.CompletedBy).HasMaxLength(256);
        builder.Property(x => x.LastFilledByRole).HasMaxLength(30);
        builder.Property(x => x.ReturnReason).HasMaxLength(1000);
        builder.Property(x => x.ReturnReasonCode).HasMaxLength(SurveyReturnReasons.MaxCodeLength);
        builder.Property(x => x.ReturnedBy).HasMaxLength(256);
        builder.Property(x => x.AdditionalDataJson).IsRequired().HasDefaultValue("{}");
        builder.Property(x => x.ResultSummaryJson).IsRequired().HasDefaultValue("{}");

        builder.HasIndex(x => x.SurveyCode)
            .IsUnique()
            .HasDatabaseName($"IX_{FsmsTableNames.Surveys}_SurveyCode");

        // The worklist is read by status, and by branch within a status.
        builder.HasIndex(x => x.Status)
            .HasDatabaseName($"IX_{FsmsTableNames.Surveys}_Status");

        builder.HasIndex(x => new { x.BranchCode, x.Status })
            .HasDatabaseName($"IX_{FsmsTableNames.Surveys}_BranchCode_Status");

        // Scope filtering narrows the worklist by CBU or operation area before anything else runs,
        // so both need to be seekable on their own — the branch index above cannot serve either.
        builder.HasIndex(x => new { x.CbuCode, x.Status })
            .HasDatabaseName($"IX_{FsmsTableNames.Surveys}_CbuCode_Status");

        builder.HasIndex(x => x.OperationAreaCode)
            .HasDatabaseName($"IX_{FsmsTableNames.Surveys}_OperationAreaCode");

        builder.HasIndex(x => x.TaskCode)
            .HasDatabaseName($"IX_{FsmsTableNames.Surveys}_TaskCode");

        // The WFM intake job asks "do we already have this FA?" once per polled row, and the
        // reconciliation job looks surveys up the same way. Without this both are table scans.
        builder.HasIndex(x => x.FaId)
            .HasDatabaseName($"IX_{FsmsTableNames.Surveys}_FaId");

        builder.HasIndex(x => x.TemplateId)
            .HasDatabaseName($"IX_{FsmsTableNames.Surveys}_TemplateId");

        builder.HasIndex(x => x.DepartmentId)
            .HasDatabaseName($"IX_{FsmsTableNames.Surveys}_DepartmentId");

        builder.HasIndex(x => x.TaskTypeId)
            .HasDatabaseName($"IX_{FsmsTableNames.Surveys}_TaskTypeId");

        builder.HasIndex(x => x.CustomerTypeId)
            .HasDatabaseName($"IX_{FsmsTableNames.Surveys}_CustomerTypeId");

        builder.HasIndex(x => x.IsExternalTask)
            .HasDatabaseName($"IX_{FsmsTableNames.Surveys}_IsExternalTask");

        builder.HasMany(x => x.StatusHistory)
            .WithOne()
            .HasForeignKey(h => h.SurveyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Assignments)
            .WithOne()
            .HasForeignKey(a => a.SurveyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Assignments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
