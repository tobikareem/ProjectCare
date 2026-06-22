using CarePath.Domain.Entities.Transitions;
using CarePath.Domain.Enumerations;
using FluentAssertions;

namespace CarePath.Domain.Tests.Entities.Transitions;

public class TransitionPlanTests
{
    // ── IsActive ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsActive_ReturnsTrue_WhenStatusIsActiveAndWithinWindow()
    {
        var plan = new TransitionPlan
        {
            Status = TransitionPlanStatus.Active,
            TransitionWindowEnd = DateTime.UtcNow.AddDays(10)
        };

        plan.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenStatusIsActiveButWindowHasEnded()
    {
        var plan = new TransitionPlan
        {
            Status = TransitionPlanStatus.Active,
            TransitionWindowEnd = DateTime.UtcNow.AddDays(-1)
        };

        plan.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenStatusIsDraftEvenIfWithinWindow()
    {
        var plan = new TransitionPlan
        {
            Status = TransitionPlanStatus.Draft,
            TransitionWindowEnd = DateTime.UtcNow.AddDays(10)
        };

        plan.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenStatusIsCompleted()
    {
        var plan = new TransitionPlan
        {
            Status = TransitionPlanStatus.Completed,
            TransitionWindowEnd = DateTime.UtcNow.AddDays(5)
        };

        plan.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenStatusIsCancelled()
    {
        var plan = new TransitionPlan
        {
            Status = TransitionPlanStatus.Cancelled,
            TransitionWindowEnd = DateTime.UtcNow.AddDays(5)
        };

        plan.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenStatusIsPendingVerification()
    {
        var plan = new TransitionPlan
        {
            Status = TransitionPlanStatus.PendingVerification,
            TransitionWindowEnd = DateTime.UtcNow.AddDays(5)
        };

        plan.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_ReturnsTrue_WhenWindowEndsExactlyNow()
    {
        // Boundary: window ending exactly at UtcNow should still be active (<=)
        var now = DateTime.UtcNow;
        var plan = new TransitionPlan
        {
            Status = TransitionPlanStatus.Active,
            TransitionWindowEnd = now
        };

        // Allow a tiny tolerance for test execution time
        plan.IsActive.Should().BeTrue();
    }

    // ── DaysRemaining ──────────────────────────────────────────────────────────

    [Fact]
    public void DaysRemaining_ReturnsCorrectDays_WhenWindowIsInFuture()
    {
        var plan = new TransitionPlan
        {
            TransitionWindowEnd = DateTime.UtcNow.AddDays(15)
        };

        plan.DaysRemaining.Should().BeInRange(14, 15);
    }

    [Fact]
    public void DaysRemaining_ReturnsZero_WhenWindowHasEnded()
    {
        var plan = new TransitionPlan
        {
            TransitionWindowEnd = DateTime.UtcNow.AddDays(-5)
        };

        plan.DaysRemaining.Should().Be(0);
    }

    [Fact]
    public void DaysRemaining_NeverReturnsNegative_WhenWindowIsLongPast()
    {
        var plan = new TransitionPlan
        {
            TransitionWindowEnd = DateTime.UtcNow.AddDays(-100)
        };

        plan.DaysRemaining.Should().Be(0);
    }

    [Fact]
    public void DaysRemaining_Returns30_OnDayOfDischarge()
    {
        var plan = new TransitionPlan
        {
            DischargeDate = DateTime.UtcNow,
            TransitionWindowEnd = DateTime.UtcNow.AddDays(30)
        };

        plan.DaysRemaining.Should().BeInRange(29, 30);
    }
}
