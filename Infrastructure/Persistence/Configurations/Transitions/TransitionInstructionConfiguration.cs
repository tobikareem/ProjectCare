using CarePath.Domain.Entities.Transitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Transitions;

/// <summary>
/// EF Core configuration for <see cref="TransitionInstruction"/>.
/// </summary>
public sealed class TransitionInstructionConfiguration : IEntityTypeConfiguration<TransitionInstruction>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TransitionInstruction> builder)
    {
        builder.ToTable("TransitionInstructions");

        builder.HasKey(instruction => instruction.Id);

        builder.Property(instruction => instruction.InstructionText).HasMaxLength(2000).IsRequired();
        builder.Property(instruction => instruction.SourceText).HasColumnType("nvarchar(max)");
        builder.Property(instruction => instruction.ConfidenceScore).HasPrecision(5, 4);
        builder.Property(instruction => instruction.ClinicalNote).HasMaxLength(2000);
        builder.Property(instruction => instruction.CreatedBy).HasMaxLength(256);
        builder.Property(instruction => instruction.UpdatedBy).HasMaxLength(256);

        builder.Ignore(instruction => instruction.IsLowConfidence);

        builder
            .HasOne(instruction => instruction.TransitionPlan)
            .WithMany(plan => plan.Instructions)
            .HasForeignKey(instruction => instruction.TransitionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(instruction => instruction.TransitionPlanId)
            .HasDatabaseName("IX_TransitionInstructions_TransitionPlanId");
        builder.HasIndex(instruction => instruction.Status).HasDatabaseName("IX_TransitionInstructions_Status");
        builder.HasIndex(instruction => instruction.IsDeleted).HasDatabaseName("IX_TransitionInstructions_IsDeleted");
    }
}
