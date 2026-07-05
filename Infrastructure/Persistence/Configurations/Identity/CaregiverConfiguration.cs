using CarePath.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// EF Core configuration for <see cref="Caregiver"/>.
/// </summary>
public sealed class CaregiverConfiguration : IEntityTypeConfiguration<Caregiver>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Caregiver> builder)
    {
        builder.ToTable("Caregivers");

        builder.HasKey(caregiver => caregiver.Id);

        builder.Property(caregiver => caregiver.HourlyPayRate).HasPrecision(18, 2);
        builder.Property(caregiver => caregiver.AverageRating).HasPrecision(3, 2);
        builder.Property(caregiver => caregiver.CreatedBy).HasMaxLength(256);
        builder.Property(caregiver => caregiver.UpdatedBy).HasMaxLength(256);

        builder
            .HasOne(caregiver => caregiver.User)
            .WithOne()
            .HasForeignKey<Caregiver>(caregiver => caregiver.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(caregiver => caregiver.Certifications)
            .WithOne(certification => certification.Caregiver)
            .HasForeignKey(certification => certification.CaregiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(caregiver => caregiver.Shifts)
            .WithOne(shift => shift.Caregiver)
            .HasForeignKey(shift => shift.CaregiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(caregiver => caregiver.VisitNotes)
            .WithOne(visitNote => visitNote.Caregiver)
            .HasForeignKey(visitNote => visitNote.CaregiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(caregiver => caregiver.UserId).IsUnique();
        builder.HasIndex(caregiver => caregiver.EmploymentType);
        builder.HasIndex(caregiver => caregiver.HireDate);
        builder.HasIndex(caregiver => caregiver.TerminationDate);
        builder.HasIndex(caregiver => caregiver.IsDeleted);
    }
}
