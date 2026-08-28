using KH.Domain.Entities.Fsms.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations.Fsms;

public sealed class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersion>
{
    public void Configure(EntityTypeBuilder<TemplateVersion> builder)
    {
        builder.ToTable(FsmsTableNames.TemplateVersions);

        builder.Property(x => x.TargetClient).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SchemaJson).IsRequired();
        builder.Property(x => x.TemplateSnapshotJson).IsRequired();

        builder.HasIndex(x => new { x.TemplateId, x.VersionNo, x.TargetClient })
            .IsUnique()
            .HasDatabaseName($"IX_{FsmsTableNames.TemplateVersions}_Template_Version_Client");
    }
}
