using CarePath.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Scheduling;

/// <summary>
/// EF Core configuration for <see cref="Shift"/>.
/// </summary>
public sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts");

        builder.HasKey(shift => shift.Id);

        builder.Property(shift => shift.BillRate).HasPrecision(18, 2);
        builder.Property(shift => shift.PayRate).HasPrecision(18, 2);
        builder.Property(shift => shift.OvertimePayRate).HasPrecision(18, 2);
        builder.Property(shift => shift.WeekendPremium).HasPrecision(18, 2);
        builder.Property(shift => shift.HolidayPremium).HasPrecision(18, 2);
        builder.Property(shift => shift.Notes).HasMaxLength(1000);
        builder.Property(shift => shift.CancellationReason).HasMaxLength(500);
        builder.Property(shift => shift.CreatedBy).HasMaxLength(100);
        builder.Property(shift => shift.UpdatedBy).HasMaxLength(100);

        builder.Ignore(shift => shift.ScheduledDuration);
        builder.Ignore(shift => shift.ActualDuration);
        builder.Ignore(shift => shift.BillableHours);
        builder.Ignore(shift => shift.GrossMargin);
        builder.Ignore(shift => shift.GrossMarginPercentage);

        builder
            .HasOne(shift => shift.Client)
            .WithMany(client => client.Shifts)
            .HasForeignKey(shift => shift.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(shift => shift.Caregiver)
            .WithMany(caregiver => caregiver.Shifts)
            .HasForeignKey(shift => shift.CaregiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(shift => shift.VisitNotes)
            .WithOne(note => note.Shift)
            .HasForeignKey(note => note.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(shift => shift.ClientId).HasDatabaseName("IX_Shifts_ClientId");
        builder.HasIndex(shift => shift.CaregiverId).HasDatabaseName("IX_Shifts_CaregiverId");
        builder.HasIndex(shift => shift.Status).HasDatabaseName("IX_Shifts_Status");
        builder.HasIndex(shift => shift.ServiceType).HasDatabaseName("IX_Shifts_ServiceType");
        builder.HasIndex(shift => shift.ScheduledStartTime).HasDatabaseName("IX_Shifts_ScheduledStartTime");
        builder.HasIndex(shift => shift.IsDeleted).HasDatabaseName("IX_Shifts_IsDeleted");
    }
}
