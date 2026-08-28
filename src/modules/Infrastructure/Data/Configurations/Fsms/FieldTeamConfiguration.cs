using KH.Domain.Entities.Fsms.Lookups;
using KH.Domain.Entities.Fsms.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class FieldTeamConfiguration : IEntityTypeConfiguration<FieldTeam>
{
    public void Configure(EntityTypeBuilder<FieldTeam> builder)
    {
        builder.ToTable(FsmsTableNames.Teams);

        builder.Property(x => x.UserCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Mobile).HasMaxLength(30);
        builder.Property(x => x.TeamType).HasMaxLength(50);
        builder.Property(x => x.ContractorId);
        builder.Property(x => x.Departments).HasMaxLength(500);

        builder.HasOne<FsmsContractor>()
            .WithMany()
            .HasForeignKey(x => x.ContractorId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.DeviceName).HasMaxLength(FieldTeam.DeviceNameMaxLength);
        builder.Property(x => x.DeviceUuid).HasMaxLength(FieldTeam.DeviceUuidMaxLength);
        builder.Property(x => x.AppVersion).HasMaxLength(FieldTeam.AppVersionMaxLength);
        builder.Property(x => x.DeviceOs).HasMaxLength(FieldTeam.DeviceOsMaxLength);

        builder.HasIndex(x => x.UserCode).HasDatabaseName($"IX_{FsmsTableNames.Teams}_UserCode");
    }
}
