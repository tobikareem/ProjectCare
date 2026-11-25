using CarePath.Domain.Entities.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.ControlPlane.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="OrganizationDataPlane"/>.</summary>
public sealed class OrganizationDataPlaneConfiguration : IEntityTypeConfiguration<OrganizationDataPlane>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OrganizationDataPlane> builder)
    {
        builder.ToTable("OrganizationDataPlanes");
        builder.HasKey(dataPlane => dataPlane.Id);

        builder.Property(dataPlane => dataPlane.Provider).HasConversion<string>().HasMaxLength(32).IsRequired();
        // A key into configuration or a secret manager — never a connection string.
        builder.Property(dataPlane => dataPlane.SecretReference).HasMaxLength(200).IsRequired();
        builder.Property(dataPlane => dataPlane.SchemaVersion).HasMaxLength(64);
        builder.Property(dataPlane => dataPlane.CreatedBy).HasMaxLength(256);
        builder.Property(dataPlane => dataPlane.UpdatedBy).HasMaxLength(256);

        builder
            .HasOne(dataPlane => dataPlane.Organization)
            .WithOne()
            .HasForeignKey<OrganizationDataPlane>(dataPlane => dataPlane.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(dataPlane => dataPlane.OrganizationId).IsUnique();
    }
}
