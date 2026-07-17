using CarePath.Domain.Entities.Billing;
using FluentAssertions;
using Xunit;

namespace CarePath.Domain.Tests.Entities;

/// <summary>
/// Pins the D-S6-18 currency rule at its source of truth: each invoice line total is rounded
/// to two decimals away from zero, and invoice subtotals are sums of rounded lines.
/// </summary>
public sealed class Sprint6BillingRoundingTests
{
    [Fact]
    public void Total_WhenProductHasSubCentPrecision_RoundsAwayFromZero()
    {
        var lineItem = new InvoiceLineItem
        {
            BillableHours = 1.25m,
            RatePerHour = 33.33m,
        };

        lineItem.Total.Should().Be(41.66m, "1.25 × 33.33 = 41.6625 rounds to 41.66");
    }

    [Fact]
    public void Total_WhenProductLandsOnAMidpoint_RoundsAwayFromZeroNotToEven()
    {
        var lineItem = new InvoiceLineItem
        {
            BillableHours = 0.25m,
            RatePerHour = 0.02m,
        };

        lineItem.Total.Should().Be(0.01m, "0.005 midpoints round away from zero, not banker's rounding");
    }

    [Fact]
    public void Subtotal_IsTheSumOfRoundedLines_NotTheRoundedSum()
    {
        var invoice = new Invoice { Id = Guid.NewGuid() };
        invoice.LineItems.Add(new InvoiceLineItem { BillableHours = 1.25m, RatePerHour = 33.33m });
        invoice.LineItems.Add(new InvoiceLineItem { BillableHours = 1.25m, RatePerHour = 33.33m });

        invoice.Subtotal.Should().Be(83.32m, "two rounded 41.66 lines sum to 83.32, not round(83.325) = 83.33");
    }
}
