using KH.Domain.Entities.Fsms.Org;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class OrgScopeConfiguration : IEntityTypeConfiguration<OrgScope>
{
    public void Configure(EntityTypeBuilder<OrgScope> builder)
    {
        builder.ToTable(FsmsTableNames.OrgScopes);

        builder.Property(x => x.OwnerType).HasMaxLength(20).IsRequired();

        // Wide enough for an Identity GUID; a team or template id is far shorter.
        builder.Property(x => x.OwnerId).HasMaxLength(450).IsRequired();

        // Level/Code are nullable together: a row may name only a department, meaning that
        // department everywhere. The pairing is enforced by OrgScope.Create, which the database
        // cannot express.
        builder.Property(x => x.Level).HasMaxLength(20);
        builder.Property(x => x.Code).HasMaxLength(50);

        // Every read is "give me this owner's scopes" — that shape, not the id alone.
        builder.HasIndex(x => new { x.OwnerType, x.OwnerId })
            .HasDatabaseName($"IX_{FsmsTableNames.OrgScopes}_Owner");

        // The same slice of coverage twice would double-count nothing but confuse every screen that
        // lists an owner's scopes, so the database refuses it rather than each command remembering
        // to. SQL Server treats NULLs as equal in a unique index, which is what is wanted here: one
        // "all departments in RCBU" row per owner, not many.
        builder.HasIndex(x => new { x.OwnerType, x.OwnerId, x.Level, x.Code, x.DepartmentId })
            .IsUnique()
            .HasDatabaseName($"IX_{FsmsTableNames.OrgScopes}_Owner_Level_Code_Department");
    }
}
