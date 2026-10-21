using CarePath.Domain.Entities.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.ControlPlane.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="Organization"/>.</summary>
public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.HasKey(organization => organization.Id);

        builder.Property(organization => organization.LegalName).HasMaxLength(200).IsRequired();
        builder.Property(organization => organization.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(organization => organization.Slug).HasMaxLength(40).IsRequired();
        builder.Property(organization => organization.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(organization => organization.ProvisioningState).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(organization => organization.DefaultTimeZone).HasMaxLength(64).IsRequired();
        builder.Property(organization => organization.DataRegion).HasMaxLength(32).IsRequired();
        builder.Property(organization => organization.SuspensionReason).HasMaxLength(200);
        builder.Property(organization => organization.CreatedBy).HasMaxLength(256);
        builder.Property(organization => organization.UpdatedBy).HasMaxLength(256);

        builder.Ignore(organization => organization.IsAdmissible);

        builder.HasIndex(organization => organization.Slug).IsUnique();
        builder.HasIndex(organization => organization.Status);
    }
}
