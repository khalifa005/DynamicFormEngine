using KH.Domain.Entities.Fsms.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class FieldTeamDepartmentConfiguration : IEntityTypeConfiguration<FieldTeamDepartment>
{
    public void Configure(EntityTypeBuilder<FieldTeamDepartment> builder)
    {
        builder.ToTable(FsmsTableNames.TeamDepartments);

        builder.HasIndex(x => new { x.TeamId, x.DepartmentId })
            .IsUnique()
            .HasDatabaseName($"IX_{FsmsTableNames.TeamDepartments}_TeamId_DepartmentId");
    }
}
