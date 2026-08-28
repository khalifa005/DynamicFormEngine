using KH.Domain.Entities.Fsms.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class TeamScheduleConfiguration : IEntityTypeConfiguration<TeamSchedule>
{
    public void Configure(EntityTypeBuilder<TeamSchedule> builder)
    {
        builder.ToTable(FsmsTableNames.TeamSchedules);

        builder.Property(x => x.WorkBranch).HasMaxLength(50);

        builder.HasIndex(x => new { x.TeamId, x.ScheduleDate })
            .HasDatabaseName($"IX_{FsmsTableNames.TeamSchedules}_Team_Date");
    }
}
