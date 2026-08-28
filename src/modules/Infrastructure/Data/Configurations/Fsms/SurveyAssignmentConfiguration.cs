using KH.Domain.Entities.Fsms.Surveys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class SurveyAssignmentConfiguration : IEntityTypeConfiguration<SurveyAssignment>
{
    public void Configure(EntityTypeBuilder<SurveyAssignment> builder)
    {
        builder.ToTable(FsmsTableNames.SurveyAssignments);

        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.AssignedBy).HasMaxLength(256);
        builder.Property(x => x.Note).HasMaxLength(1000);

        builder.HasIndex(x => x.SurveyId)
            .HasDatabaseName($"IX_{FsmsTableNames.SurveyAssignments}_SurveyId");

        // A team's own worklist reads its open assignments.
        builder.HasIndex(x => new { x.FieldTeamId, x.Status })
            .HasDatabaseName($"IX_{FsmsTableNames.SurveyAssignments}_FieldTeamId_Status");
    }
}
