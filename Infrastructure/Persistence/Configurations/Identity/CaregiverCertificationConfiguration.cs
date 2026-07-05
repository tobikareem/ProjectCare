using CarePath.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// EF Core configuration for <see cref="CaregiverCertification"/>.
/// </summary>
public sealed class CaregiverCertificationConfiguration : IEntityTypeConfiguration<CaregiverCertification>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CaregiverCertification> builder)
    {
        builder.ToTable("CaregiverCertifications");

        builder.HasKey(certification => certification.Id);

        builder.Property(certification => certification.CertificationNumber).HasMaxLength(50);
        builder.Property(certification => certification.IssuingAuthority).HasMaxLength(100);
        builder.Property(certification => certification.CreatedBy).HasMaxLength(256);
        builder.Property(certification => certification.UpdatedBy).HasMaxLength(256);

        builder.Ignore(certification => certification.IsExpired);
        builder.Ignore(certification => certification.IsExpiringSoon);

        builder
            .HasOne(certification => certification.Caregiver)
            .WithMany(caregiver => caregiver.Certifications)
            .HasForeignKey(certification => certification.CaregiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(certification => certification.CaregiverId);
        builder.HasIndex(certification => certification.Type);
        builder.HasIndex(certification => certification.ExpirationDate);
        builder.HasIndex(certification => certification.IsDeleted);
    }
}
