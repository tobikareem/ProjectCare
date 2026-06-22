using CarePath.Domain.Entities.Transitions;
using CarePath.Domain.Enumerations;
using FluentAssertions;

namespace CarePath.Domain.Tests.Entities.Transitions;

public class TransitionReminderTests
{
    // ── IsOverdue ──────────────────────────────────────────────────────────────

    [Fact]
    public void IsOverdue_ReturnsTrue_WhenScheduledAndScheduledAtIsInThePast()
    {
        var reminder = new TransitionReminder
        {
            Status = ReminderStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-30)
        };

        reminder.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public void IsOverdue_ReturnsFalse_WhenScheduledAndScheduledAtIsInTheFuture()
    {
        var reminder = new TransitionReminder
        {
            Status = ReminderStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };

        reminder.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_ReturnsFalse_WhenStatusIsSentEvenIfScheduledAtIsInThePast()
    {
        var reminder = new TransitionReminder
        {
            Status = ReminderStatus.Sent,
            ScheduledAt = DateTime.UtcNow.AddHours(-1)
        };

        reminder.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_ReturnsFalse_WhenStatusIsAcknowledged()
    {
        var reminder = new TransitionReminder
        {
            Status = ReminderStatus.Acknowledged,
            ScheduledAt = DateTime.UtcNow.AddHours(-1)
        };

        reminder.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_ReturnsFalse_WhenStatusIsMissed()
    {
        var reminder = new TransitionReminder
        {
            Status = ReminderStatus.Missed,
            ScheduledAt = DateTime.UtcNow.AddHours(-1)
        };

        reminder.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_ReturnsFalse_WhenStatusIsFailed()
    {
        var reminder = new TransitionReminder
        {
            Status = ReminderStatus.Failed,
            ScheduledAt = DateTime.UtcNow.AddHours(-1)
        };

        reminder.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_ReturnsFalse_WhenScheduledExactlyNow()
    {
        // A reminder scheduled at exactly now is not yet overdue (strict less-than)
        var reminder = new TransitionReminder
        {
            Status = ReminderStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddSeconds(5) // slight buffer for test execution
        };

        reminder.IsOverdue.Should().BeFalse();
    }
}
