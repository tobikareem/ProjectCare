using CarePath.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// EF Core configuration for <see cref="User"/>.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    private readonly SqlDialect _dialect;

    /// <summary>Initializes the configuration with the active provider's SQL dialect.</summary>
    /// <param name="dialect">Dialect used to build provider-valid filtered index expressions.</param>
    public UserConfiguration(SqlDialect dialect)
    {
        _dialect = dialect;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(user => user.LastName).HasMaxLength(100).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(256).IsRequired();
        builder.Property(user => user.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(user => user.Address).HasMaxLength(200);
        builder.Property(user => user.City).HasMaxLength(100);
        builder.Property(user => user.State).HasMaxLength(50);
        builder.Property(user => user.ZipCode).HasMaxLength(10);
        builder.Property(user => user.CreatedBy).HasMaxLength(256);
        builder.Property(user => user.UpdatedBy).HasMaxLength(256);

        builder.Ignore(user => user.FullName);

        builder.HasIndex(user => user.Email).IsUnique();
        builder.HasIndex(user => user.Role);
        builder.HasIndex(user => user.IsActive);
        builder.HasIndex(user => user.IsDeleted);
        builder.HasIndex(user => user.CreatedAt);

        // CP-04: link to the platform identity (equal to Id by construction). Nullable until the
        // CP-05 tightening migration (D-CP04-07); unique among the rows that carry it.
        builder.HasIndex(user => user.PlatformUserId)
            .IsUnique()
            .HasFilter(_dialect.IsNotNull(nameof(User.PlatformUserId)));
    }
}
