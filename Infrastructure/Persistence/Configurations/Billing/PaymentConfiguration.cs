using CarePath.Domain.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Billing;

/// <summary>
/// EF Core configuration for <see cref="Payment"/>.
/// </summary>
public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.ReferenceNumber).HasMaxLength(100);
        builder.Property(payment => payment.Notes).HasMaxLength(1000);
        builder.Property(payment => payment.FailureReason).HasMaxLength(500);
        builder.Property(payment => payment.CreatedBy).HasMaxLength(100);
        builder.Property(payment => payment.UpdatedBy).HasMaxLength(100);

        builder
            .HasOne(payment => payment.Invoice)
            .WithMany(invoice => invoice.Payments)
            .HasForeignKey(payment => payment.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(payment => payment.InvoiceId).HasDatabaseName("IX_Payments_InvoiceId");
        builder.HasIndex(payment => payment.PaymentDate).HasDatabaseName("IX_Payments_PaymentDate");
        builder.HasIndex(payment => payment.Method).HasDatabaseName("IX_Payments_Method");
        builder.HasIndex(payment => payment.Status).HasDatabaseName("IX_Payments_Status");
        builder.HasIndex(payment => payment.ReferenceNumber).HasDatabaseName("IX_Payments_ReferenceNumber");
        builder.HasIndex(payment => payment.IsDeleted).HasDatabaseName("IX_Payments_IsDeleted");
    }
}
