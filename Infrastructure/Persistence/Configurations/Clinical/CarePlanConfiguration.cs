using CarePath.Domain.Entities.Clinical;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Clinical;

/// <summary>
/// EF Core configuration for <see cref="CarePlan"/>.
/// </summary>
public sealed class CarePlanConfiguration : IEntityTypeConfiguration<CarePlan>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CarePlan> builder)
    {
        builder.ToTable("CarePlans");

        builder.HasKey(plan => plan.Id);

        builder.Property(plan => plan.Title).HasMaxLength(200).IsRequired();
        builder.Property(plan => plan.Description).HasMaxLength(1000);
        builder.Property(plan => plan.Goals).HasMaxLength(2000);
        builder.Property(plan => plan.Interventions).HasMaxLength(2000);
        builder.Property(plan => plan.Notes).HasMaxLength(2000);
        builder.Property(plan => plan.CreatedBy).HasMaxLength(100);
        builder.Property(plan => plan.UpdatedBy).HasMaxLength(100);

        builder
            .HasOne(plan => plan.Client)
            .WithMany(client => client.CarePlans)
            .HasForeignKey(plan => plan.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(plan => plan.ClientId).HasDatabaseName("IX_CarePlans_ClientId");
        builder.HasIndex(plan => plan.IsActive).HasDatabaseName("IX_CarePlans_IsActive");
        builder.HasIndex(plan => plan.StartDate).HasDatabaseName("IX_CarePlans_StartDate");
        builder.HasIndex(plan => plan.IsDeleted).HasDatabaseName("IX_CarePlans_IsDeleted");
    }
}
