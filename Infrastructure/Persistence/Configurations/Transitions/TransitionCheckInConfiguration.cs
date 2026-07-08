using CarePath.Domain.Entities.Transitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Transitions;

/// <summary>
/// EF Core configuration for <see cref="TransitionCheckIn"/>.
/// </summary>
public sealed class TransitionCheckInConfiguration : IEntityTypeConfiguration<TransitionCheckIn>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TransitionCheckIn> builder)
    {
        builder.ToTable("TransitionCheckIns");

        builder.HasKey(checkIn => checkIn.Id);

        // Unbounded string; SQL Server infers nvarchar(max). No explicit column type so
        // provider-agnostic test databases (SQLite) can create the schema.
        builder.Property(checkIn => checkIn.ResponsesJson).IsRequired();
        builder.Property(checkIn => checkIn.CreatedBy).HasMaxLength(256);
        builder.Property(checkIn => checkIn.UpdatedBy).HasMaxLength(256);

        builder
            .HasOne(checkIn => checkIn.TransitionPlan)
            .WithMany(plan => plan.CheckIns)
            .HasForeignKey(checkIn => checkIn.TransitionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(checkIn => checkIn.TransitionPlanId)
            .HasDatabaseName("IX_TransitionCheckIns_TransitionPlanId");
        builder.HasIndex(checkIn => checkIn.IsDeleted).HasDatabaseName("IX_TransitionCheckIns_IsDeleted");
    }
}
