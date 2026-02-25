using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using FluentAssertions;

namespace CarePath.Domain.Tests.Entities;

public class ShiftTests
{
    private static Shift ShiftWithActualTimes(
        DateTime start, DateTime end, int breakMinutes = 0,
        decimal billRate = 35m, decimal payRate = 18m)
        => new()
        {
            ScheduledStartTime = start,
            ScheduledEndTime = end,
            ActualStartTime = start,
            ActualEndTime = end,
            BreakMinutes = breakMinutes,
            BillRate = billRate,
            PayRate = payRate
        };

    // ── BillableHours ──────────────────────────────────────────────────────────

    [Fact]
    public void BillableHours_ReturnsZero_WhenActualStartTimeIsNull()
    {
        var shift = new Shift { ActualEndTime = DateTime.UtcNow };
        shift.BillableHours.Should().Be(0m);
    }

    [Fact]
    public void BillableHours_ReturnsZero_WhenActualEndTimeIsNull()
    {
        var shift = new Shift { ActualStartTime = DateTime.UtcNow };
        shift.BillableHours.Should().Be(0m);
    }

    [Fact]
    public void BillableHours_ReturnsZero_WhenBothActualTimesAreNull()
    {
        var shift = new Shift();
        shift.BillableHours.Should().Be(0m);
    }

    [Fact]
    public void BillableHours_CalculatesCorrectly_WithNoBreak()
    {
        var start = new DateTime(2026, 2, 16, 9, 0, 0, DateTimeKind.Utc);
        var end   = new DateTime(2026, 2, 16, 17, 0, 0, DateTimeKind.Utc);
        var shift = ShiftWithActualTimes(start, end, breakMinutes: 0);

        shift.BillableHours.Should().Be(8m);
    }

    [Fact]
    public void BillableHours_CalculatesCorrectly_WithBreak()
    {
        var start = new DateTime(2026, 2, 16, 9, 0, 0, DateTimeKind.Utc);
        var end   = new DateTime(2026, 2, 16, 17, 0, 0, DateTimeKind.Utc);
        var shift = ShiftWithActualTimes(start, end, breakMinutes: 30);

        shift.BillableHours.Should().Be(7.5m);
    }

    [Fact]
    public void BillableHours_ReturnsZero_WhenBreakExceedsDuration()
    {
        var start = new DateTime(2026, 2, 16, 9, 0, 0, DateTimeKind.Utc);
        var end   = new DateTime(2026, 2, 16, 9, 30, 0, DateTimeKind.Utc);
        var shift = ShiftWithActualTimes(start, end, breakMinutes: 60);

        shift.BillableHours.Should().Be(0m);
    }

    [Fact]
    public void BillableHours_ReturnsZero_WhenBreakEqualsDuration()
    {
        var start = new DateTime(2026, 2, 16, 9, 0, 0, DateTimeKind.Utc);
        var end   = new DateTime(2026, 2, 16, 10, 0, 0, DateTimeKind.Utc);
        var shift = ShiftWithActualTimes(start, end, breakMinutes: 60);

        shift.BillableHours.Should().Be(0m);
    }

    // ── ScheduledDuration ─────────────────────────────────────────────────────

    [Fact]
    public void ScheduledDuration_CalculatesCorrectly()
    {
        var start = new DateTime(2026, 2, 16, 9, 0, 0, DateTimeKind.Utc);
        var end   = new DateTime(2026, 2, 16, 17, 0, 0, DateTimeKind.Utc);
        var shift = new Shift { ScheduledStartTime = start, ScheduledEndTime = end };

        shift.ScheduledDuration.Should().Be(TimeSpan.FromHours(8));
    }

    // ── ActualDuration ────────────────────────────────────────────────────────

    [Fact]
    public void ActualDuration_ReturnsNull_WhenTimesNotRecorded()
    {
        var shift = new Shift();
        shift.ActualDuration.Should().BeNull();
    }

    [Fact]
    public void ActualDuration_CalculatesCorrectly_WhenBothTimesSet()
    {
        var start = new DateTime(2026, 2, 16, 9, 0, 0, DateTimeKind.Utc);
        var end   = new DateTime(2026, 2, 16, 17, 0, 0, DateTimeKind.Utc);
        var shift = new Shift { ActualStartTime = start, ActualEndTime = end };

        shift.ActualDuration.Should().Be(TimeSpan.FromHours(8));
    }

    // ── GrossMargin ───────────────────────────────────────────────────────────

    [Fact]
    public void GrossMargin_CalculatesCorrectly_ForCompletedShift()
    {
        // 8 hrs @ $35 bill / $18 pay → margin = (35-18) * 8 = $136
        var start = new DateTime(2026, 2, 16, 9, 0, 0, DateTimeKind.Utc);
        var end   = new DateTime(2026, 2, 16, 17, 0, 0, DateTimeKind.Utc);
        var shift = ShiftWithActualTimes(start, end, billRate: 35m, payRate: 18m);

        shift.GrossMargin.Should().Be(136m);
    }

    [Fact]
    public void GrossMargin_ReturnsZero_WhenShiftNotCompleted()
    {
        var shift = new Shift { BillRate = 35m, PayRate = 18m };
        shift.GrossMargin.Should().Be(0m);
    }

    // ── GrossMarginPercentage ─────────────────────────────────────────────────

    [Fact]
    public void GrossMarginPercentage_CalculatesCorrectly_ForCompletedShift()
    {
        // (35-18)/35 * 100 = 48.571...%
        var start = new DateTime(2026, 2, 16, 9, 0, 0, DateTimeKind.Utc);
        var end   = new DateTime(2026, 2, 16, 17, 0, 0, DateTimeKind.Utc);
        var shift = ShiftWithActualTimes(start, end, billRate: 35m, payRate: 18m);

        shift.GrossMarginPercentage.Should().BeApproximately(48.57m, 0.01m);
    }

    [Fact]
    public void GrossMarginPercentage_ReturnsZero_WhenBillRateIsZero()
    {
        var start = new DateTime(2026, 2, 16, 9, 0, 0, DateTimeKind.Utc);
        var end   = new DateTime(2026, 2, 16, 17, 0, 0, DateTimeKind.Utc);
        var shift = ShiftWithActualTimes(start, end, billRate: 0m, payRate: 18m);

        shift.GrossMarginPercentage.Should().Be(0m);
    }

    [Fact]
    public void GrossMarginPercentage_ReturnsZero_WhenShiftNotCompleted()
    {
        var shift = new Shift { BillRate = 35m, PayRate = 18m };
        shift.GrossMarginPercentage.Should().Be(0m);
    }

    [Fact]
    public void GrossMarginPercentage_MeetsW2Target_ForInHomeCare()
    {
        // In-home (W-2): target 40-45%; $35 bill / $20 pay = 42.86%
        var start = new DateTime(2026, 2, 16, 9, 0, 0, DateTimeKind.Utc);
        var end   = new DateTime(2026, 2, 16, 17, 0, 0, DateTimeKind.Utc);
        var shift = ShiftWithActualTimes(start, end, billRate: 35m, payRate: 20m);

        shift.GrossMarginPercentage.Should().BeInRange(40m, 45m);
    }

    [Fact]
    public void GrossMarginPercentage_MeetsContractorTarget_ForFacilityStaffing()
    {
        // Facility (1099): target 25-30%; $50 bill / $36 pay = 28%
        var start = new DateTime(2026, 2, 16, 9, 0, 0, DateTimeKind.Utc);
        var end   = new DateTime(2026, 2, 16, 17, 0, 0, DateTimeKind.Utc);
        var shift = ShiftWithActualTimes(start, end, billRate: 50m, payRate: 36m);

        shift.GrossMarginPercentage.Should().BeInRange(25m, 30m);
    }

    // ── Defaults ──────────────────────────────────────────────────────────────

    [Fact]
    public void Status_DefaultsToScheduled()
    {
        var shift = new Shift();
        shift.Status.Should().Be(ShiftStatus.Scheduled);
    }

    [Fact]
    public void BreakMinutes_DefaultsToZero()
    {
        var shift = new Shift();
        shift.BreakMinutes.Should().Be(0);
    }

    [Fact]
    public void VisitNotes_DefaultsToEmptyCollection()
    {
        var shift = new Shift();
        shift.VisitNotes.Should().NotBeNull().And.BeEmpty();
    }
}
