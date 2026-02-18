using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using FluentAssertions;

namespace CarePath.Domain.Tests.Business;

/// <summary>
/// Scenario-level tests that validate margin targets across multiple shift configurations.
/// These complement the per-property unit tests in <c>ShiftTests</c>:
/// ShiftTests verifies the GrossMargin / GrossMarginPercentage formulas in isolation;
/// this file validates that real-world rate combinations meet the 40-45% W-2 and
/// 25-30% 1099 business targets described in the design spec.
/// </summary>
public class MarginCalculationTests
{
    private static Shift CompletedShift(decimal billRate, decimal payRate, int durationHours = 8)
    {
        var start = new DateTime(2026, 2, 16, 9, 0, 0, DateTimeKind.Utc);
        return new Shift
        {
            BillRate = billRate,
            PayRate = payRate,
            ScheduledStartTime = start,
            ScheduledEndTime = start.AddHours(durationHours),
            ActualStartTime = start,
            ActualEndTime = start.AddHours(durationHours),
            BreakMinutes = 0
        };
    }

    // ── W-2 (In-Home) Margin Targets: 40-45% ─────────────────────────────────

    [Fact]
    public void W2Shift_GrossMarginPercentage_IsWithinTarget_LowerBound()
    {
        // $35 bill / $21 pay = 40% margin
        var shift = CompletedShift(billRate: 35m, payRate: 21m);
        shift.GrossMarginPercentage.Should().BeApproximately(40m, 0.01m);
    }

    [Fact]
    public void W2Shift_GrossMarginPercentage_IsWithinTarget_UpperBound()
    {
        // $40 bill / $22 pay = 45% margin
        var shift = CompletedShift(billRate: 40m, payRate: 22m);
        shift.GrossMarginPercentage.Should().BeApproximately(45m, 0.01m);
    }

    [Fact]
    public void W2Shift_GrossMarginPercentage_MidRange_IsWithinTarget()
    {
        // $35 bill / $20 pay ≈ 42.86% (within 40-45%)
        var shift = CompletedShift(billRate: 35m, payRate: 20m);
        shift.GrossMarginPercentage.Should().BeInRange(40m, 45m);
    }

    // ── 1099 (Facility) Margin Targets: 25-30% ───────────────────────────────

    [Fact]
    public void ContractorShift_GrossMarginPercentage_IsWithinTarget_LowerBound()
    {
        // $50 bill / $37.50 pay = 25% margin
        var shift = CompletedShift(billRate: 50m, payRate: 37.50m);
        shift.GrossMarginPercentage.Should().BeApproximately(25m, 0.01m);
    }

    [Fact]
    public void ContractorShift_GrossMarginPercentage_IsWithinTarget_UpperBound()
    {
        // $50 bill / $35 pay = 30% margin
        var shift = CompletedShift(billRate: 50m, payRate: 35m);
        shift.GrossMarginPercentage.Should().BeApproximately(30m, 0.01m);
    }

    // ── Absolute GrossMargin (Total, Not Per-Hour) ────────────────────────────

    [Fact]
    public void GrossMargin_ScalesWithBillableHours()
    {
        var fourHourShift = CompletedShift(billRate: 35m, payRate: 18m, durationHours: 4);
        var eightHourShift = CompletedShift(billRate: 35m, payRate: 18m, durationHours: 8);

        // (35-18)*4 = 68; (35-18)*8 = 136
        fourHourShift.GrossMargin.Should().Be(68m);
        eightHourShift.GrossMargin.Should().Be(136m);
        eightHourShift.GrossMargin.Should().Be(fourHourShift.GrossMargin * 2);
    }

    [Fact]
    public void GrossMarginPercentage_IsIndependentOfShiftDuration()
    {
        // Percentage is (BillRate - PayRate) / BillRate * 100, hours cancel out
        var fourHourShift  = CompletedShift(billRate: 35m, payRate: 18m, durationHours: 4);
        var eightHourShift = CompletedShift(billRate: 35m, payRate: 18m, durationHours: 8);

        // decimal arithmetic has no floating-point drift — expect exact equality
        fourHourShift.GrossMarginPercentage.Should().Be(eightHourShift.GrossMarginPercentage);
    }

    // ── Multi-Shift Aggregation ───────────────────────────────────────────────

    [Fact]
    public void MultipleShifts_TotalGrossMargin_SumsCorrectly()
    {
        var shifts = new[]
        {
            CompletedShift(billRate: 35m, payRate: 18m, durationHours: 8),  // $136
            CompletedShift(billRate: 35m, payRate: 18m, durationHours: 4),  // $68
            CompletedShift(billRate: 40m, payRate: 22m, durationHours: 6),  // $108
        };

        var totalGrossMargin = shifts.Sum(s => s.GrossMargin);
        totalGrossMargin.Should().Be(312m);  // 136 + 68 + 108
    }

    [Fact]
    public void UncompletedShift_ContributesZeroGrossMargin()
    {
        var uncompletedShift = new Shift { BillRate = 35m, PayRate = 18m };
        uncompletedShift.GrossMargin.Should().Be(0m);
    }

    // ── Break-Time Impact on Margin ───────────────────────────────────────────

    [Fact]
    public void BreakTime_ReducesTotalGrossMargin()
    {
        var start = new DateTime(2026, 2, 16, 9, 0, 0, DateTimeKind.Utc);
        var shiftWithBreak = new Shift
        {
            BillRate = 35m,
            PayRate = 18m,
            ScheduledStartTime = start,
            ScheduledEndTime = start.AddHours(8),
            ActualStartTime = start,
            ActualEndTime = start.AddHours(8),
            BreakMinutes = 30  // 7.5 billable hours
        };

        // (35-18) * 7.5 = $127.50
        shiftWithBreak.GrossMargin.Should().Be(127.5m);
    }
}
