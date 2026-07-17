using CarePath.Domain.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Billing;

/// <summary>
/// EF Core configuration for the append-only <see cref="BillingReconciliationResolution"/>
/// (D-S6-18). Both foreign keys restrict deletes so resolution history can never be destroyed
/// by a parent removal.
/// </summary>
public sealed class BillingReconciliationResolutionConfiguration : IEntityTypeConfiguration<BillingReconciliationResolution>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BillingReconciliationResolution> builder)
    {
        builder.ToTable("BillingReconciliationResolutions");

        builder.HasKey(resolution => resolution.Id);

        builder.Property(resolution => resolution.Note).HasMaxLength(500);
        builder.Property(resolution => resolution.CreatedBy).HasMaxLength(100);
        builder.Property(resolution => resolution.UpdatedBy).HasMaxLength(100);

        builder
            .HasOne(resolution => resolution.Shift)
            .WithMany()
            .HasForeignKey(resolution => resolution.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(resolution => resolution.SupersedesResolution)
            .WithMany()
            .HasForeignKey(resolution => resolution.SupersedesResolutionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Latest-decision-per-shift lookups drive eligibility classification and history reads.
        builder.HasIndex(resolution => new { resolution.ShiftId, resolution.ResolvedAtUtc })
            .HasDatabaseName("IX_BillingReconciliationResolutions_Shift_ResolvedAt");
        builder.HasIndex(resolution => resolution.IsDeleted)
            .HasDatabaseName("IX_BillingReconciliationResolutions_IsDeleted");
    }
}
