using CarePath.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// EF Core configuration for <see cref="Client"/>.
/// </summary>
public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");

        builder.HasKey(client => client.Id);

        builder.Property(client => client.EmergencyContactName).HasMaxLength(100);
        builder.Property(client => client.EmergencyContactPhone).HasMaxLength(20);
        builder.Property(client => client.EmergencyContactRelationship).HasMaxLength(100);
        builder.Property(client => client.SpecialInstructions).HasMaxLength(1000);
        builder.Property(client => client.MedicalConditions).HasMaxLength(1000);
        builder.Property(client => client.Allergies).HasMaxLength(500);
        builder.Property(client => client.LocationNotes).HasMaxLength(500);
        builder.Property(client => client.InsuranceProvider).HasMaxLength(100);
        builder.Property(client => client.InsurancePolicyNumber).HasMaxLength(50);
        builder.Property(client => client.MedicaidNumber).HasMaxLength(50);
        builder.Property(client => client.HourlyBillRate).HasPrecision(18, 2);
        builder.Property(client => client.CreatedBy).HasMaxLength(256);
        builder.Property(client => client.UpdatedBy).HasMaxLength(256);

        builder.Ignore(client => client.Age);

        builder
            .HasOne(client => client.User)
            .WithOne()
            .HasForeignKey<Client>(client => client.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(client => client.Shifts)
            .WithOne(shift => shift.Client)
            .HasForeignKey(shift => shift.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(client => client.CarePlans)
            .WithOne(carePlan => carePlan.Client)
            .HasForeignKey(carePlan => carePlan.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(client => client.Invoices)
            .WithOne(invoice => invoice.Client)
            .HasForeignKey(invoice => invoice.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(client => client.UserId).IsUnique();
        builder.HasIndex(client => client.DateOfBirth);
        builder.HasIndex(client => client.ServiceType);
        builder.HasIndex(client => client.IsDeleted);
    }
}
