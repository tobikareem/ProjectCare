using CarePath.Domain.Entities.Transitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Transitions;

/// <summary>
/// EF Core configuration for <see cref="TransitionEscalation"/>.
/// </summary>
public sealed class TransitionEscalationConfiguration : IEntityTypeConfiguration<TransitionEscalation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TransitionEscalation> builder)
    {
        builder.ToTable("TransitionEscalations");

        builder.HasKey(escalation => escalation.Id);

        builder.Property(escalation => escalation.TriggerDetails).HasMaxLength(1000).IsRequired();
        builder.Property(escalation => escalation.ResolutionNote).HasMaxLength(2000);
        builder.Property(escalation => escalation.CreatedBy).HasMaxLength(256);
        builder.Property(escalation => escalation.UpdatedBy).HasMaxLength(256);

        builder
            .HasOne(escalation => escalation.TransitionPlan)
            .WithMany(plan => plan.Escalations)
            .HasForeignKey(escalation => escalation.TransitionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(escalation => escalation.TransitionPlanId)
            .HasDatabaseName("IX_TransitionEscalations_TransitionPlanId");
        builder.HasIndex(escalation => escalation.IsDeleted).HasDatabaseName("IX_TransitionEscalations_IsDeleted");
    }
}
