using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Core.Entities;

namespace KH.Infrastructure.Data.Configurations;

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.CorrelationId)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnType("varchar(50)");

        builder.Property(e => e.CallerType)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnType("varchar(20)");

        builder.Property(e => e.UserId)
            .HasMaxLength(100)
            .HasColumnType("varchar(100)");

        builder.Property(e => e.AppName)
            .HasMaxLength(100)
            .HasColumnType("varchar(100)");

        builder.Property(e => e.ClientIp)
            .IsRequired()
            .HasMaxLength(45)
            .HasColumnType("varchar(45)");

        builder.Property(e => e.Endpoint)
            .IsRequired()
            .HasMaxLength(1024)
            .HasColumnType("varchar(1024)");

        builder.Property(e => e.HttpMethod)
            .IsRequired()
            .HasMaxLength(10)
            .HasColumnType("varchar(10)");

        builder.Property(e => e.RequestBody)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.ResponseBody)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.EventName)
            .HasMaxLength(100)
            .HasColumnType("varchar(100)");

        builder.Property(e => e.StatusCode)
            .IsRequired();

        builder.Property(e => e.IsSuccess)
            .IsRequired();

        builder.Property(e => e.DurationMs)
            .IsRequired();

        builder.Property(e => e.RequestedAt)
            .IsRequired()
            .HasColumnType("datetime2");

        builder.Property(e => e.RespondedAt)
            .IsRequired()
            .HasColumnType("datetime2");

        // Indexes for optimal lookup performance
        builder.HasIndex(e => e.CorrelationId)
            .HasDatabaseName("IX_AuditLogs_CorrelationId");

        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_AuditLogs_UserId");

        builder.HasIndex(e => e.RequestedAt)
            .HasDatabaseName("IX_AuditLogs_RequestedAt");
    }
}
