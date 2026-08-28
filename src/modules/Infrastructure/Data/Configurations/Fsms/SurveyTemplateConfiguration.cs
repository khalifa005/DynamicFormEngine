using KH.Domain.Entities.Fsms.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class SurveyTemplateConfiguration : IEntityTypeConfiguration<SurveyTemplate>
{
    public void Configure(EntityTypeBuilder<SurveyTemplate> builder)
    {
        builder.ToTable(FsmsTableNames.Templates);

        builder.Property(x => x.TemplateCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.TemplateNameEn).HasMaxLength(250).IsRequired();
        builder.Property(x => x.TemplateNameAr).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.BranchScope).HasMaxLength(500);
        builder.Property(x => x.FaTypeCode).HasMaxLength(50);
        builder.Property(x => x.TeamFillSlaHours).IsRequired();
        builder.Property(x => x.CompletionSlaHours).IsRequired();
        builder.Property(x => x.DefinitionJson).IsRequired().HasDefaultValue("{}");

        // Existing templates keep the pin-and-migrate-by-hand behaviour they were created under.
        builder.Property(x => x.AutoMigrateSurveysOnPublish).IsRequired().HasDefaultValue(false);

        builder.HasIndex(x => x.TemplateCode)
            .IsUnique()
            .HasDatabaseName($"IX_{FsmsTableNames.Templates}_TemplateCode");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName($"IX_{FsmsTableNames.Templates}_Status");

        builder.HasIndex(x => x.Category)
            .HasDatabaseName($"IX_{FsmsTableNames.Templates}_Category");

        builder.HasIndex(x => x.DepartmentId)
            .HasDatabaseName($"IX_{FsmsTableNames.Templates}_DepartmentId");

        builder.HasIndex(x => new { x.Status, x.Category })
            .HasDatabaseName($"IX_{FsmsTableNames.Templates}_Status_Category");

        // An inbound survey resolves its template by FA type within the published ones, so the pair
        // is what the lookup reads.
        builder.HasIndex(x => new { x.FaTypeCode, x.Status })
            .HasDatabaseName($"IX_{FsmsTableNames.Templates}_FaTypeCode_Status");

        builder.HasMany(x => x.Versions)
            .WithOne()
            .HasForeignKey(v => v.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
