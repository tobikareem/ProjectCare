using CarePath.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// EF Core configuration for <see cref="ClientAccessGrant"/>.
/// </summary>
public sealed class ClientAccessGrantConfiguration : IEntityTypeConfiguration<ClientAccessGrant>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ClientAccessGrant> builder)
    {
        builder.ToTable("ClientAccessGrants");

        builder.HasKey(grant => grant.Id);

        builder.Property(grant => grant.AccessScope).HasConversion<int>();
        builder.Property(grant => grant.CreatedBy).HasMaxLength(256);
        builder.Property(grant => grant.UpdatedBy).HasMaxLength(256);

        builder.Ignore(grant => grant.IsRevoked);

        builder
            .HasOne(grant => grant.GranteeUser)
            .WithMany()
            .HasForeignKey(grant => grant.GranteeUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(grant => grant.Client)
            .WithMany()
            .HasForeignKey(grant => grant.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(grant => grant.GrantedByUser)
            .WithMany()
            .HasForeignKey(grant => grant.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(grant => grant.RevokedByUser)
            .WithMany()
            .HasForeignKey(grant => grant.RevokedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(grant => new { grant.GranteeUserId, grant.ClientId });
        builder.HasIndex(grant => grant.ClientId);
        builder.HasIndex(grant => grant.IsDeleted);
    }
}
