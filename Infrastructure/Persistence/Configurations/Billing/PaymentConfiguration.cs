using CarePath.Domain.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Billing;

/// <summary>
/// EF Core configuration for <see cref="Payment"/>.
/// </summary>
public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    private readonly SqlDialect _dialect;

    /// <summary>Initializes the configuration with the active provider's SQL dialect.</summary>
    /// <param name="dialect">Dialect used to build provider-valid filtered index expressions.</param>
    public PaymentConfiguration(SqlDialect dialect)
    {
        _dialect = dialect;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.ReferenceNumber).HasMaxLength(100);
        builder.Property(payment => payment.Notes).HasMaxLength(1000);
        builder.Property(payment => payment.FailureReason).HasMaxLength(500);
        builder.Property(payment => payment.CreatedBy).HasMaxLength(256);
        builder.Property(payment => payment.UpdatedBy).HasMaxLength(256);

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

        // One settlement per (invoice, receipt reference): the database backstop for the
        // idempotent RecordPayment path. Null references stay free-form.
        builder.HasIndex(payment => new { payment.InvoiceId, payment.ReferenceNumber })
            .IsUnique()
            .HasDatabaseName("UX_Payments_InvoiceId_ReferenceNumber")
            .HasFilter($"{_dialect.IsNotNull(nameof(Payment.ReferenceNumber))} AND {_dialect.IsFalse(nameof(Payment.IsDeleted))}");
        builder.HasIndex(payment => payment.IsDeleted).HasDatabaseName("IX_Payments_IsDeleted");
    }
}
