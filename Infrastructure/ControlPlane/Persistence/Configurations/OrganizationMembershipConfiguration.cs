using CarePath.Domain.Entities.Platform;
using CarePath.Infrastructure.ControlPlane.Identity;
using CarePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.ControlPlane.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="OrganizationMembership"/>.</summary>
public sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    private readonly SqlDialect _dialect;

    /// <summary>Initializes the configuration with the active provider's SQL dialect.</summary>
    /// <param name="dialect">Dialect used to build provider-valid filtered index expressions.</param>
    public OrganizationMembershipConfiguration(SqlDialect dialect)
    {
        _dialect = dialect;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("OrganizationMemberships");
        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(membership => membership.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        // Incremented with a set-based ExecuteUpdate (SecurityVersion + 1) so concurrent changes
        // never lose an increment; a concurrency token is deliberately not used.
        builder.Property(membership => membership.SecurityVersion).IsRequired();
        builder.Property(membership => membership.CreatedBy).HasMaxLength(256);
        builder.Property(membership => membership.UpdatedBy).HasMaxLength(256);

        builder.Ignore(membership => membership.IsActive);

        builder
            .HasOne(membership => membership.Organization)
            .WithMany()
            .HasForeignKey(membership => membership.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Same database as the Identity user, so the link is a real foreign key (the cross-database
        // exception in D-CP04-02 applies only to the tenant User row).
        builder
            .HasOne<PlatformUser>()
            .WithMany()
            .HasForeignKey(membership => membership.PlatformUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // One live membership per platform user per organization; the authoritative role record.
        builder.HasIndex(membership => new { membership.OrganizationId, membership.PlatformUserId })
            .IsUnique()
            .HasFilter(_dialect.IsFalse(nameof(OrganizationMembership.IsDeleted)));
        builder.HasIndex(membership => membership.PlatformUserId);
    }
}
