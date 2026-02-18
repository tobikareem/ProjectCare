using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Enumerations;
using FluentAssertions;

namespace CarePath.Domain.Tests.Integration;

public class InvoicePaymentNavigationTests
{
    [Fact]
    public void Invoice_LineItemsCollection_CanContainMultipleItems()
    {
        var invoice = new Invoice();
        invoice.LineItems.Add(new InvoiceLineItem { BillableHours = 8m, RatePerHour = 35m });
        invoice.LineItems.Add(new InvoiceLineItem { BillableHours = 4m, RatePerHour = 35m });

        invoice.LineItems.Should().HaveCount(2);
        invoice.Subtotal.Should().Be(420m);  // 280 + 140
    }

    [Fact]
    public void Invoice_PaymentsCollection_CanContainMultiplePayments()
    {
        var invoice = new Invoice();
        invoice.Payments.Add(new Payment { Amount = 100m, Status = PaymentStatus.Settled });
        invoice.Payments.Add(new Payment { Amount = 50m, Status = PaymentStatus.Settled });

        invoice.Payments.Should().HaveCount(2);
        invoice.AmountPaid.Should().Be(150m);
    }

    [Fact]
    public void Invoice_ComputedProperties_UpdateWhenCollectionsChange()
    {
        var invoice = new Invoice { TaxAmount = 0m };
        invoice.LineItems.Add(new InvoiceLineItem { BillableHours = 8m, RatePerHour = 35m });

        var initialBalance = invoice.Balance;  // $280

        invoice.Payments.Add(new Payment { Amount = 100m, Status = PaymentStatus.Settled });

        invoice.Balance.Should().Be(initialBalance - 100m);
        invoice.IsFullyPaid.Should().BeFalse();
    }

    [Fact]
    public void Payment_LinksBackToInvoice_ViaNavigationProperty()
    {
        var invoice = new Invoice();
        var payment = new Payment { InvoiceId = invoice.Id, Invoice = invoice, Amount = 100m };
        invoice.Payments.Add(payment);

        payment.Invoice.Should().BeSameAs(invoice);
        payment.InvoiceId.Should().Be(invoice.Id);
    }

    [Fact]
    public void InvoiceLineItem_LinksToShift_WhenShiftLinked()
    {
        var invoice = new Invoice();
        var shiftId = Guid.NewGuid();
        var item = new InvoiceLineItem
        {
            InvoiceId = invoice.Id,
            Invoice = invoice,
            ShiftId = shiftId,
            BillableHours = 8m,
            RatePerHour = 35m
        };

        item.ShiftId.Should().Be(shiftId);
    }

    [Fact]
    public void InvoiceLineItem_ShiftId_IsNullForManualLineItems()
    {
        var item = new InvoiceLineItem { BillableHours = 1m, RatePerHour = 50m };
        item.ShiftId.Should().BeNull();
    }
}
