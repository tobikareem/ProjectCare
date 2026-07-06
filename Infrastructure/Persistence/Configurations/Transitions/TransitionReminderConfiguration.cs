using CarePath.Domain.Entities.Transitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Transitions;

/// <summary>
/// EF Core configuration for <see cref="TransitionReminder"/>.
/// </summary>
public sealed class TransitionReminderConfiguration : IEntityTypeConfiguration<TransitionReminder>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TransitionReminder> builder)
    {
        builder.ToTable("TransitionReminders");

        builder.HasKey(reminder => reminder.Id);

        builder.Property(reminder => reminder.CreatedBy).HasMaxLength(256);
        builder.Property(reminder => reminder.UpdatedBy).HasMaxLength(256);

        builder.Ignore(reminder => reminder.IsOverdue);

        builder
            .HasOne(reminder => reminder.TransitionPlan)
            .WithMany(plan => plan.Reminders)
            .HasForeignKey(reminder => reminder.TransitionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<TransitionInstruction>()
            .WithMany()
            .HasForeignKey(reminder => reminder.TransitionInstructionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(reminder => reminder.TransitionPlanId)
            .HasDatabaseName("IX_TransitionReminders_TransitionPlanId");
        builder.HasIndex(reminder => reminder.TransitionInstructionId)
            .HasDatabaseName("IX_TransitionReminders_TransitionInstructionId");
        builder.HasIndex(reminder => reminder.Status).HasDatabaseName("IX_TransitionReminders_Status");
        builder.HasIndex(reminder => reminder.ScheduledAt).HasDatabaseName("IX_TransitionReminders_ScheduledAt");
        builder.HasIndex(reminder => reminder.IsDeleted).HasDatabaseName("IX_TransitionReminders_IsDeleted");
    }
}
