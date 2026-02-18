using CarePath.Domain.Entities.Billing;
using FluentAssertions;

namespace CarePath.Domain.Tests.Entities;

public class InvoiceLineItemTests
{
    // ── Total ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Total_CalculatesCorrectly()
    {
        var item = new InvoiceLineItem { BillableHours = 8m, RatePerHour = 35m };
        item.Total.Should().Be(280m);
    }

    [Fact]
    public void Total_IsZero_WhenBillableHoursIsZero()
    {
        var item = new InvoiceLineItem { BillableHours = 0m, RatePerHour = 35m };
        item.Total.Should().Be(0m);
    }

    // ── TotalCost ─────────────────────────────────────────────────────────────

    [Fact]
    public void TotalCost_ReturnsNull_WhenCostPerHourNotSet()
    {
        var item = new InvoiceLineItem { BillableHours = 8m, RatePerHour = 35m };
        item.TotalCost.Should().BeNull();
    }

    [Fact]
    public void TotalCost_CalculatesCorrectly_WhenCostPerHourIsSet()
    {
        var item = new InvoiceLineItem
        {
            BillableHours = 8m,
            RatePerHour = 35m,
            CostPerHour = 18m
        };
        item.TotalCost.Should().Be(144m);  // 8 * 18
    }

    // ── GrossProfit ───────────────────────────────────────────────────────────

    [Fact]
    public void GrossProfit_ReturnsNull_WhenCostPerHourNotSet()
    {
        var item = new InvoiceLineItem { BillableHours = 8m, RatePerHour = 35m };
        item.GrossProfit.Should().BeNull();
    }

    [Fact]
    public void GrossProfit_CalculatesCorrectly()
    {
        var item = new InvoiceLineItem
        {
            BillableHours = 8m,
            RatePerHour = 35m,
            CostPerHour = 18m
        };
        // Total=$280, TotalCost=$144 → GrossProfit=$136
        item.GrossProfit.Should().Be(136m);
    }

    // ── GrossMarginPercentage ─────────────────────────────────────────────────

    [Fact]
    public void GrossMarginPercentage_ReturnsNull_WhenCostPerHourNotSet()
    {
        var item = new InvoiceLineItem { BillableHours = 8m, RatePerHour = 35m };
        item.GrossMarginPercentage.Should().BeNull();
    }

    [Fact]
    public void GrossMarginPercentage_ReturnsNull_WhenTotalIsZero()
    {
        var item = new InvoiceLineItem
        {
            BillableHours = 0m,
            RatePerHour = 35m,
            CostPerHour = 18m
        };
        item.GrossMarginPercentage.Should().BeNull();
    }

    [Fact]
    public void GrossMarginPercentage_CalculatesCorrectly()
    {
        var item = new InvoiceLineItem
        {
            BillableHours = 8m,
            RatePerHour = 35m,
            CostPerHour = 18m
        };
        // GrossProfit=$136, Total=$280 → 136/280*100 = 48.571...%
        item.GrossMarginPercentage.Should().BeApproximately(48.57m, 0.01m);
    }
}
