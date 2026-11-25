using CarePath.Domain.Entities.Platform;
using CarePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.ControlPlane.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="OrganizationDomain"/>.</summary>
public sealed class OrganizationDomainConfiguration : IEntityTypeConfiguration<OrganizationDomain>
{
    private readonly SqlDialect _dialect;

    /// <summary>Initializes the configuration with the active provider's SQL dialect.</summary>
    /// <param name="dialect">Dialect used to build provider-valid filtered index expressions.</param>
    public OrganizationDomainConfiguration(SqlDialect dialect)
    {
        _dialect = dialect;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OrganizationDomain> builder)
    {
        builder.ToTable("OrganizationDomains");
        builder.HasKey(domain => domain.Id);

        builder.Property(domain => domain.HostName).HasMaxLength(253).IsRequired();
        builder.Property(domain => domain.VerificationStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(domain => domain.CreatedBy).HasMaxLength(256);
        builder.Property(domain => domain.UpdatedBy).HasMaxLength(256);

        builder.Ignore(domain => domain.IsResolvable);

        builder
            .HasOne(domain => domain.Organization)
            .WithMany()
            .HasForeignKey(domain => domain.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Hot-path lookup for host resolution; unique among live rows so a soft-removed host can
        // be registered again, and one live host never maps to two organizations.
        builder.HasIndex(domain => domain.HostName)
            .IsUnique()
            .HasFilter(_dialect.IsFalse(nameof(OrganizationDomain.IsDeleted)));
        builder.HasIndex(domain => new { domain.OrganizationId, domain.IsPrimary });
    }
}
