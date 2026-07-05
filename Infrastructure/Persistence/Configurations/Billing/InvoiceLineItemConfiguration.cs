using CarePath.Domain.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarePath.Infrastructure.Persistence.Configurations.Billing;

/// <summary>
/// EF Core configuration for <see cref="InvoiceLineItem"/>.
/// </summary>
public sealed class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("InvoiceLineItems");

        builder.HasKey(lineItem => lineItem.Id);

        builder.Property(lineItem => lineItem.Description).HasMaxLength(500).IsRequired();
        builder.Property(lineItem => lineItem.BillableHours).HasPrecision(18, 2);
        builder.Property(lineItem => lineItem.RatePerHour).HasPrecision(18, 2);
        builder.Property(lineItem => lineItem.CostPerHour).HasPrecision(18, 2);
        builder.Property(lineItem => lineItem.CreatedBy).HasMaxLength(100);
        builder.Property(lineItem => lineItem.UpdatedBy).HasMaxLength(100);

        builder.Ignore(lineItem => lineItem.Total);
        builder.Ignore(lineItem => lineItem.TotalCost);
        builder.Ignore(lineItem => lineItem.GrossProfit);
        builder.Ignore(lineItem => lineItem.GrossMarginPercentage);

        builder
            .HasOne(lineItem => lineItem.Invoice)
            .WithMany(invoice => invoice.LineItems)
            .HasForeignKey(lineItem => lineItem.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(lineItem => lineItem.Shift)
            .WithMany()
            .HasForeignKey(lineItem => lineItem.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(lineItem => lineItem.InvoiceId).HasDatabaseName("IX_InvoiceLineItems_InvoiceId");
        builder.HasIndex(lineItem => lineItem.ShiftId).HasDatabaseName("IX_InvoiceLineItems_ShiftId");
        builder.HasIndex(lineItem => lineItem.ServiceDate).HasDatabaseName("IX_InvoiceLineItems_ServiceDate");
        builder.HasIndex(lineItem => lineItem.IsDeleted).HasDatabaseName("IX_InvoiceLineItems_IsDeleted");
    }
}
