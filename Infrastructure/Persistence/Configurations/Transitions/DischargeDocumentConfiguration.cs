using CarePath.Domain.Entities.Transitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Transitions;

/// <summary>
/// EF Core configuration for <see cref="DischargeDocument"/>.
/// </summary>
public sealed class DischargeDocumentConfiguration : IEntityTypeConfiguration<DischargeDocument>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DischargeDocument> builder)
    {
        builder.ToTable("DischargeDocuments");

        builder.HasKey(document => document.Id);

        builder.Property(document => document.RawContent).HasColumnType("nvarchar(max)");
        builder.Property(document => document.SourceReference).HasMaxLength(200);
        builder.Property(document => document.CreatedBy).HasMaxLength(256);
        builder.Property(document => document.UpdatedBy).HasMaxLength(256);

        builder
            .HasOne(document => document.Client)
            .WithMany()
            .HasForeignKey(document => document.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(document => document.TransitionPlans)
            .WithOne(plan => plan.DischargeDocument)
            .HasForeignKey(plan => plan.DischargeDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(document => document.TransitionPlans)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(document => document.ClientId).HasDatabaseName("IX_DischargeDocuments_ClientId");
        builder.HasIndex(document => document.Status).HasDatabaseName("IX_DischargeDocuments_Status");
        builder.HasIndex(document => document.IsDeleted).HasDatabaseName("IX_DischargeDocuments_IsDeleted");
    }
}
