using CarePath.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Scheduling;

/// <summary>
/// EF Core configuration for <see cref="VisitNote"/>.
/// </summary>
public sealed class VisitNoteConfiguration : IEntityTypeConfiguration<VisitNote>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VisitNote> builder)
    {
        builder.ToTable("VisitNotes");

        builder.HasKey(note => note.Id);

        builder.Property(note => note.Activities).HasMaxLength(4000);
        builder.Property(note => note.ClientCondition).HasMaxLength(4000);
        builder.Property(note => note.Concerns).HasMaxLength(4000);
        builder.Property(note => note.Medications).HasMaxLength(4000);
        builder.Property(note => note.Temperature).HasPrecision(5, 2);
        builder.Property(note => note.CaregiverSignatureUrl).HasMaxLength(500);
        builder.Property(note => note.ClientOrFamilySignatureUrl).HasMaxLength(500);
        builder.Property(note => note.CreatedBy).HasMaxLength(100);
        builder.Property(note => note.UpdatedBy).HasMaxLength(100);

        builder.Ignore(note => note.TransitionPlanId);

        builder
            .HasOne(note => note.Shift)
            .WithMany(shift => shift.VisitNotes)
            .HasForeignKey(note => note.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(note => note.Caregiver)
            .WithMany(caregiver => caregiver.VisitNotes)
            .HasForeignKey(note => note.CaregiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(note => note.Photos)
            .WithOne(photo => photo.VisitNote)
            .HasForeignKey(photo => photo.VisitNoteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(note => note.ShiftId).HasDatabaseName("IX_VisitNotes_ShiftId");
        builder.HasIndex(note => note.CaregiverId).HasDatabaseName("IX_VisitNotes_CaregiverId");
        builder.HasIndex(note => note.VisitDateTime).HasDatabaseName("IX_VisitNotes_VisitDateTime");
        builder.HasIndex(note => note.IsDeleted).HasDatabaseName("IX_VisitNotes_IsDeleted");
    }
}
