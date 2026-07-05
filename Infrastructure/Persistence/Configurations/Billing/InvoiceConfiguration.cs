using CarePath.Domain.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Billing;

/// <summary>
/// EF Core configuration for <see cref="Invoice"/>.
/// </summary>
public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.HasKey(invoice => invoice.Id);

        builder.Property(invoice => invoice.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(invoice => invoice.TaxAmount).HasPrecision(18, 2);
        builder.Property(invoice => invoice.Notes).HasMaxLength(1000);
        builder.Property(invoice => invoice.CreatedBy).HasMaxLength(100);
        builder.Property(invoice => invoice.UpdatedBy).HasMaxLength(100);

        builder.Ignore(invoice => invoice.Subtotal);
        builder.Ignore(invoice => invoice.Total);
        builder.Ignore(invoice => invoice.AmountPaid);
        builder.Ignore(invoice => invoice.Balance);
        builder.Ignore(invoice => invoice.IsFullyPaid);

        builder
            .HasOne(invoice => invoice.Client)
            .WithMany(client => client.Invoices)
            .HasForeignKey(invoice => invoice.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(invoice => invoice.LineItems)
            .WithOne(lineItem => lineItem.Invoice)
            .HasForeignKey(lineItem => lineItem.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(invoice => invoice.Payments)
            .WithOne(payment => payment.Invoice)
            .HasForeignKey(payment => payment.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(invoice => invoice.InvoiceNumber).IsUnique().HasDatabaseName("IX_Invoices_InvoiceNumber");
        builder.HasIndex(invoice => invoice.ClientId).HasDatabaseName("IX_Invoices_ClientId");
        builder.HasIndex(invoice => invoice.InvoiceDate).HasDatabaseName("IX_Invoices_InvoiceDate");
        builder.HasIndex(invoice => invoice.DueDate).HasDatabaseName("IX_Invoices_DueDate");
        builder.HasIndex(invoice => invoice.Status).HasDatabaseName("IX_Invoices_Status");
        builder.HasIndex(invoice => invoice.IsDeleted).HasDatabaseName("IX_Invoices_IsDeleted");
    }
}
