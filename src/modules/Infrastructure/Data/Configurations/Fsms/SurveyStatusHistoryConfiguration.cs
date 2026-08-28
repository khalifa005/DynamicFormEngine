using KH.Domain.Entities.Fsms.Surveys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class SurveyStatusHistoryConfiguration : IEntityTypeConfiguration<SurveyStatusHistory>
{
    public void Configure(EntityTypeBuilder<SurveyStatusHistory> builder)
    {
        builder.ToTable(FsmsTableNames.SurveyStatusHistory);

        builder.Property(x => x.FromStatus).HasMaxLength(30);
        builder.Property(x => x.ToStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ChangedBy).HasMaxLength(256);
        builder.Property(x => x.Note).HasMaxLength(1000);

        // The timeline query reads a survey's trail in order.
        builder.HasIndex(x => new { x.SurveyId, x.ChangedDate })
            .HasDatabaseName($"IX_{FsmsTableNames.SurveyStatusHistory}_SurveyId_ChangedDate");
    }
}
