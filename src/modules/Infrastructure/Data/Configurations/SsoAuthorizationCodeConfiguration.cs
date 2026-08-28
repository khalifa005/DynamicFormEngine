using KH.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KH.Infrastructure.Data.Configurations;

public sealed class SsoAuthorizationCodeConfiguration : IEntityTypeConfiguration<SsoAuthorizationCode>
{
    public void Configure(EntityTypeBuilder<SsoAuthorizationCode> builder)
    {
        builder.ToTable("SM_SSO_AUTH_CODE");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(x => x.CodeHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.SessionIndex)
            .HasMaxLength(256);

        builder.HasIndex(x => x.CodeHash)
            .IsUnique()
            .HasDatabaseName("IX_SM_SSO_AUTH_CODE_CodeHash");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_SM_SSO_AUTH_CODE_UserId");
    }
}
