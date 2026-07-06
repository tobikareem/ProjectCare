using CarePath.Domain.Entities.Transitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Transitions;

/// <summary>
/// EF Core configuration for <see cref="TransitionPlan"/>.
/// </summary>
public sealed class TransitionPlanConfiguration : IEntityTypeConfiguration<TransitionPlan>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TransitionPlan> builder)
    {
        builder.ToTable("TransitionPlans");

        builder.HasKey(plan => plan.Id);

        builder.Property(plan => plan.HospitalName).HasMaxLength(100);
        builder.Property(plan => plan.CreatedBy).HasMaxLength(256);
        builder.Property(plan => plan.UpdatedBy).HasMaxLength(256);

        builder.Ignore(plan => plan.IsActive);
        builder.Ignore(plan => plan.DaysRemaining);

        builder
            .HasOne(plan => plan.Client)
            .WithMany()
            .HasForeignKey(plan => plan.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(plan => plan.DischargeDocument)
            .WithMany(document => document.TransitionPlans)
            .HasForeignKey(plan => plan.DischargeDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(plan => plan.Instructions)
            .WithOne(instruction => instruction.TransitionPlan)
            .HasForeignKey(instruction => instruction.TransitionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(plan => plan.Reminders)
            .WithOne(reminder => reminder.TransitionPlan)
            .HasForeignKey(reminder => reminder.TransitionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(plan => plan.CheckIns)
            .WithOne(checkIn => checkIn.TransitionPlan)
            .HasForeignKey(checkIn => checkIn.TransitionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(plan => plan.Escalations)
            .WithOne(escalation => escalation.TransitionPlan)
            .HasForeignKey(escalation => escalation.TransitionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(plan => plan.Instructions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(plan => plan.Reminders).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(plan => plan.CheckIns).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(plan => plan.Escalations).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(plan => plan.ClientId).HasDatabaseName("IX_TransitionPlans_ClientId");
        builder.HasIndex(plan => plan.DischargeDocumentId).HasDatabaseName("IX_TransitionPlans_DischargeDocumentId");
        builder.HasIndex(plan => plan.Status).HasDatabaseName("IX_TransitionPlans_Status");
        builder.HasIndex(plan => plan.IsDeleted).HasDatabaseName("IX_TransitionPlans_IsDeleted");
    }
}
