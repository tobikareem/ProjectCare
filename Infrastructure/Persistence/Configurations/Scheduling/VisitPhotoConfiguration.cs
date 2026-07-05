using CarePath.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Scheduling;

/// <summary>
/// EF Core configuration for <see cref="VisitPhoto"/>.
/// </summary>
public sealed class VisitPhotoConfiguration : IEntityTypeConfiguration<VisitPhoto>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VisitPhoto> builder)
    {
        builder.ToTable("VisitPhotos");

        builder.HasKey(photo => photo.Id);

        builder.Property(photo => photo.PhotoUrl).HasMaxLength(500).IsRequired();
        builder.Property(photo => photo.Caption).HasMaxLength(500);
        builder.Property(photo => photo.CreatedBy).HasMaxLength(100);
        builder.Property(photo => photo.UpdatedBy).HasMaxLength(100);

        builder
            .HasOne(photo => photo.VisitNote)
            .WithMany(note => note.Photos)
            .HasForeignKey(photo => photo.VisitNoteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(photo => photo.VisitNoteId).HasDatabaseName("IX_VisitPhotos_VisitNoteId");
        builder.HasIndex(photo => photo.TakenAt).HasDatabaseName("IX_VisitPhotos_TakenAt");
        builder.HasIndex(photo => photo.IsDeleted).HasDatabaseName("IX_VisitPhotos_IsDeleted");
    }
}
