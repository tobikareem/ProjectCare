using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Transitions;

/// <summary>
/// Coordinator request to schedule a reminder record (plan ID travels in the route). Guarded
/// server-side per D-S5-6: rejected with <c>transition.plan_not_active</c> unless the plan is
/// Active, and <c>transition.outside_window</c> when <see cref="ScheduledAt"/> falls outside
/// the transition window. Records only in Sprint 5 — no delivery.
/// </summary>
public class ScheduleReminderRequest
{
    /// <summary>Linked instruction, when the reminder tracks a specific instruction.</summary>
    public Guid? TransitionInstructionId { get; init; }

    /// <summary>Reminder type.</summary>
    public ReminderType ReminderType { get; init; }

    /// <summary>Delivery channel (delivery itself lands in Sprint 7).</summary>
    public ReminderChannel Channel { get; init; }

    /// <summary>When the reminder should fire (UTC). Must fall within the plan window.</summary>
    public DateTime ScheduledAt { get; init; }
}
