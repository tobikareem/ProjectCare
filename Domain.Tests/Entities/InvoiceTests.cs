using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Enumerations;
using FluentAssertions;

namespace CarePath.Domain.Tests.Entities;

public class InvoiceTests
{
    private static InvoiceLineItem LineItem(decimal hours, decimal ratePerHour) =>
        new() { BillableHours = hours, RatePerHour = ratePerHour };

    private static Payment SettledPayment(decimal amount) =>
        new() { Amount = amount, Status = PaymentStatus.Settled };

    private static Payment PendingPayment(decimal amount) =>
        new() { Amount = amount, Status = PaymentStatus.Pending };

    private static Payment FailedPayment(decimal amount) =>
        new() { Amount = amount, Status = PaymentStatus.Failed };

    private static Payment RefundedPayment(decimal amount) =>
        new() { Amount = amount, Status = PaymentStatus.Refunded };

    // ── Subtotal ──────────────────────────────────────────────────────────────

    [Fact]
    public void Subtotal_ReturnsZero_WhenNoLineItems()
    {
        var invoice = new Invoice();
        invoice.Subtotal.Should().Be(0m);
    }

    [Fact]
    public void Subtotal_SumsAllLineItemTotals()
    {
        var invoice = new Invoice();
        invoice.LineItems.Add(LineItem(8m, 35m));   // $280
        invoice.LineItems.Add(LineItem(4m, 35m));   // $140

        invoice.Subtotal.Should().Be(420m);
    }

    // ── Total ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Total_EqualsSubtotalPlusTaxAmount()
    {
        var invoice = new Invoice { TaxAmount = 28m };
        invoice.LineItems.Add(LineItem(8m, 35m));  // $280

        invoice.Total.Should().Be(308m);
    }

    [Fact]
    public void Total_EqualSubtotal_WhenTaxAmountIsZero()
    {
        var invoice = new Invoice { TaxAmount = 0m };
        invoice.LineItems.Add(LineItem(8m, 35m));

        invoice.Total.Should().Be(invoice.Subtotal);
    }

    // ── AmountPaid ────────────────────────────────────────────────────────────

    [Fact]
    public void AmountPaid_ReturnsZero_WhenNoPayments()
    {
        var invoice = new Invoice();
        invoice.AmountPaid.Should().Be(0m);
    }

    [Fact]
    public void AmountPaid_SumsOnlySettledPayments()
    {
        var invoice = new Invoice();
        invoice.Payments.Add(SettledPayment(100m));
        invoice.Payments.Add(SettledPayment(50m));
        invoice.Payments.Add(PendingPayment(200m));   // excluded
        invoice.Payments.Add(FailedPayment(75m));     // excluded

        invoice.AmountPaid.Should().Be(150m);
    }

    [Fact]
    public void AmountPaid_ExcludesPendingPayments()
    {
        var invoice = new Invoice();
        invoice.Payments.Add(PendingPayment(100m));

        invoice.AmountPaid.Should().Be(0m);
    }

    [Fact]
    public void AmountPaid_ExcludesFailedPayments()
    {
        var invoice = new Invoice();
        invoice.Payments.Add(FailedPayment(100m));

        invoice.AmountPaid.Should().Be(0m);
    }

    [Fact]
    public void AmountPaid_ExcludesRefundedPayments()
    {
        var invoice = new Invoice();
        invoice.Payments.Add(RefundedPayment(100m));

        invoice.AmountPaid.Should().Be(0m);
    }

    // ── Balance ───────────────────────────────────────────────────────────────

    [Fact]
    public void Balance_EqualsTotal_WhenNoPayments()
    {
        var invoice = new Invoice();
        invoice.LineItems.Add(LineItem(8m, 35m));  // $280

        invoice.Balance.Should().Be(280m);
    }

    [Fact]
    public void Balance_CalculatesCorrectly_WithPartialPayment()
    {
        var invoice = new Invoice { TaxAmount = 0m };
        invoice.LineItems.Add(LineItem(8m, 35m));  // $280
        invoice.Payments.Add(SettledPayment(100m));

        invoice.Balance.Should().Be(180m);
    }

    [Fact]
    public void Balance_IsZero_WhenFullyPaid()
    {
        var invoice = new Invoice { TaxAmount = 0m };
        invoice.LineItems.Add(LineItem(8m, 35m));  // $280
        invoice.Payments.Add(SettledPayment(280m));

        invoice.Balance.Should().Be(0m);
    }

    // ── IsFullyPaid ───────────────────────────────────────────────────────────

    [Fact]
    public void IsFullyPaid_ReturnsFalse_WhenBalanceIsPositive()
    {
        var invoice = new Invoice { TaxAmount = 0m };
        invoice.LineItems.Add(LineItem(8m, 35m));
        invoice.Payments.Add(SettledPayment(100m));

        invoice.IsFullyPaid.Should().BeFalse();
    }

    [Fact]
    public void IsFullyPaid_ReturnsTrue_WhenBalanceIsZero()
    {
        var invoice = new Invoice { TaxAmount = 0m };
        invoice.LineItems.Add(LineItem(8m, 35m));  // $280
        invoice.Payments.Add(SettledPayment(280m));

        invoice.IsFullyPaid.Should().BeTrue();
    }

    [Fact]
    public void IsFullyPaid_ReturnsTrue_WhenOverpaid()
    {
        var invoice = new Invoice { TaxAmount = 0m };
        invoice.LineItems.Add(LineItem(8m, 35m));  // $280
        invoice.Payments.Add(SettledPayment(300m));

        invoice.IsFullyPaid.Should().BeTrue();
    }

    // ── RecalculateStatus ─────────────────────────────────────────────────────

    [Fact]
    public void RecalculateStatus_SetsStatusToPaid_WhenFullyPaid()
    {
        var invoice = new Invoice { TaxAmount = 0m };
        invoice.LineItems.Add(LineItem(8m, 35m));  // $280
        invoice.Payments.Add(SettledPayment(280m));

        invoice.RecalculateStatus();

        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public void RecalculateStatus_SetsStatusToPartiallyPaid_WhenPartialPayment()
    {
        var invoice = new Invoice { TaxAmount = 0m };
        invoice.LineItems.Add(LineItem(8m, 35m));  // $280
        invoice.Payments.Add(SettledPayment(100m));

        invoice.RecalculateStatus();

        invoice.Status.Should().Be(InvoiceStatus.PartiallyPaid);
    }

    [Fact]
    public void RecalculateStatus_DoesNotChangeStatus_WhenCancelled()
    {
        var invoice = new Invoice { TaxAmount = 0m, Status = InvoiceStatus.Cancelled };
        invoice.LineItems.Add(LineItem(8m, 35m));
        invoice.Payments.Add(SettledPayment(280m));

        invoice.RecalculateStatus();

        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
    }

    [Fact]
    public void RecalculateStatus_DoesNotChangeStatus_WhenNoPaymentsMade()
    {
        var invoice = new Invoice { TaxAmount = 0m, Status = InvoiceStatus.Sent };
        invoice.LineItems.Add(LineItem(8m, 35m));

        invoice.RecalculateStatus();

        // No payment made → status stays as-is (Sent)
        invoice.Status.Should().Be(InvoiceStatus.Sent);
    }

    [Fact]
    public void RecalculateStatus_DoesNotChangeStatus_WhenDraftAndNoPayments()
    {
        var invoice = new Invoice { TaxAmount = 0m, Status = InvoiceStatus.Draft };
        invoice.LineItems.Add(LineItem(8m, 35m));

        invoice.RecalculateStatus();

        invoice.Status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public void RecalculateStatus_IsIdempotent_WhenAlreadyPaid()
    {
        var invoice = new Invoice { TaxAmount = 0m };
        invoice.LineItems.Add(LineItem(8m, 35m));  // $280
        invoice.Payments.Add(SettledPayment(280m));

        invoice.RecalculateStatus();
        invoice.RecalculateStatus();  // second call must not corrupt state

        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public void RecalculateStatus_NeverSetsOverdue_Directly()
    {
        // Overdue requires an external mechanism (e.g., scheduled job comparing DueDate to UtcNow).
        // RecalculateStatus only transitions between Paid / PartiallyPaid; it must never set Overdue.
        var invoice = new Invoice { TaxAmount = 0m, Status = InvoiceStatus.Sent };
        invoice.LineItems.Add(LineItem(8m, 35m));
        invoice.Payments.Add(SettledPayment(100m));  // partial payment

        invoice.RecalculateStatus();

        invoice.Status.Should().NotBe(InvoiceStatus.Overdue);
    }

    // ── Defaults ──────────────────────────────────────────────────────────────

    [Fact]
    public void Status_DefaultsToDraft()
    {
        var invoice = new Invoice();
        invoice.Status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public void InvoiceDate_HasNoConstructionTimeDefault()
    {
        var invoice = new Invoice();
        invoice.InvoiceDate.Should().Be(default(DateTime));
    }
}
