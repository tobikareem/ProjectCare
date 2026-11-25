using CarePath.Domain.Entities.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.ControlPlane.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="OrganizationBranding"/>.</summary>
public sealed class OrganizationBrandingConfiguration : IEntityTypeConfiguration<OrganizationBranding>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OrganizationBranding> builder)
    {
        builder.ToTable("OrganizationBrandings");
        builder.HasKey(branding => branding.Id);

        builder.Property(branding => branding.LogoStorageKey).HasMaxLength(256);
        builder.Property(branding => branding.PrimaryColorToken).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(branding => branding.AccentColorToken).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(branding => branding.SupportEmail).HasMaxLength(256);
        builder.Property(branding => branding.SupportPhone).HasMaxLength(20);
        builder.Property(branding => branding.CreatedBy).HasMaxLength(256);
        builder.Property(branding => branding.UpdatedBy).HasMaxLength(256);

        builder
            .HasOne(branding => branding.Organization)
            .WithOne()
            .HasForeignKey<OrganizationBranding>(branding => branding.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(branding => branding.OrganizationId).IsUnique();
    }
}
