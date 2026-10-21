using CarePath.Domain.Entities.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.ControlPlane.Persistence.Configurations;

/// <summary>EF Core configuration for the append-only <see cref="PlatformAuditEvent"/>.</summary>
public sealed class PlatformAuditEventConfiguration : IEntityTypeConfiguration<PlatformAuditEvent>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PlatformAuditEvent> builder)
    {
        builder.ToTable("PlatformAuditEvents");
        builder.HasKey(auditEvent => auditEvent.Id);

        builder.Property(auditEvent => auditEvent.Action).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(auditEvent => auditEvent.EntityType).HasMaxLength(64).IsRequired();
        builder.Property(auditEvent => auditEvent.CorrelationId).HasMaxLength(64);
        // SHA-256 hex of the normalized host; the host string itself is never stored.
        builder.Property(auditEvent => auditEvent.HostHash).HasMaxLength(64);
        builder.Property(auditEvent => auditEvent.CreatedBy).HasMaxLength(256);
        builder.Property(auditEvent => auditEvent.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(auditEvent => new { auditEvent.OrganizationId, auditEvent.OccurredAtUtc });
        builder.HasIndex(auditEvent => new { auditEvent.HostHash, auditEvent.OccurredAtUtc });
    }
}
