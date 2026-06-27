using CarePath.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// EF Core configuration for <see cref="ApplicationUser"/>.
/// </summary>
public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.DomainUserId).IsRequired();

        builder.Property(user => user.Email).HasMaxLength(256);
        builder.Property(user => user.UserName).HasMaxLength(256);
        builder.Property(user => user.PhoneNumber).HasMaxLength(20);

        builder
            .HasIndex(user => user.DomainUserId)
            .IsUnique()
            .HasDatabaseName("IX_AspNetUsers_DomainUserId");

        builder
            .HasOne(user => user.DomainUser)
            .WithOne()
            .HasForeignKey<ApplicationUser>(user => user.DomainUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
