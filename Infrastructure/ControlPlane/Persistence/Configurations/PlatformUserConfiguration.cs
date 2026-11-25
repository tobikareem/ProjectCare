using CarePath.Infrastructure.ControlPlane.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.ControlPlane.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="PlatformUser"/> on top of the Identity defaults.</summary>
public sealed class PlatformUserConfiguration : IEntityTypeConfiguration<PlatformUser>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PlatformUser> builder)
    {
        builder.Property(user => user.Email).HasMaxLength(256);
        builder.Property(user => user.UserName).HasMaxLength(256);
        builder.Property(user => user.PhoneNumber).HasMaxLength(20);
        builder.Property(user => user.DisplayName).HasMaxLength(100).IsRequired();

        builder.HasIndex(user => user.IsPlatformAdmin);
    }
}
