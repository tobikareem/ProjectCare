using CarePath.Domain.Entities.Platform;
using CarePath.Infrastructure.ControlPlane.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.ControlPlane.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="PlatformRefreshToken"/>.</summary>
public sealed class PlatformRefreshTokenConfiguration : IEntityTypeConfiguration<PlatformRefreshToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PlatformRefreshToken> builder)
    {
        builder.ToTable("PlatformRefreshTokens");
        builder.HasKey(token => token.Id);

        builder.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();

        builder
            .HasOne<PlatformUser>()
            .WithMany()
            .HasForeignKey(token => token.PlatformUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<OrganizationMembership>()
            .WithMany()
            .HasForeignKey(token => token.MembershipId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Organization>()
            .WithMany()
            .HasForeignKey(token => token.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => new { token.MembershipId, token.RevokedAtUtc });
        builder.HasIndex(token => token.PlatformUserId);
    }
}
