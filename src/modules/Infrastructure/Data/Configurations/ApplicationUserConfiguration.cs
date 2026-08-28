using KH.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Not a relational FK — TEAMS is mirrored from WFM and a team row can be replaced without
        // dragging the login with it. Indexed because "who is this crew's account" is a lookup.
        builder.HasIndex(x => x.FieldTeamId)
            .HasDatabaseName("IX_AspNetUsers_FieldTeamId")
            .HasFilter("[FieldTeamId] IS NOT NULL");
    }
}
