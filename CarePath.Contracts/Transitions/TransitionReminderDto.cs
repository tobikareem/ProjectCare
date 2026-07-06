using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Transitions;

/// <summary>
/// A scheduled reminder record. Sprint 5 produces records only (Status = Scheduled);
/// delivery lands in Sprint 7. Reminder message content is never logged.
/// </summary>
public class TransitionReminderDto
{
    /// <summary>Reminder identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Owning transition plan.</summary>
    public Guid TransitionPlanId { get; init; }

    /// <summary>Linked instruction, when the reminder tracks a specific instruction.</summary>
    public Guid? TransitionInstructionId { get; init; }

    /// <summary>Reminder type.</summary>
    public ReminderType ReminderType { get; init; }

    /// <summary>Delivery channel.</summary>
    public ReminderChannel Channel { get; init; }

    /// <summary>When the reminder is scheduled to fire (UTC). Must fall within the plan window (D-S5-6).</summary>
    public DateTime ScheduledAt { get; init; }

    /// <summary>When it was sent (UTC), once delivery exists.</summary>
    public DateTime? SentAt { get; init; }

    /// <summary>When the patient acknowledged it (UTC).</summary>
    public DateTime? AcknowledgedAt { get; init; }

    /// <summary>Delivery status.</summary>
    public ReminderStatus Status { get; init; }

    /// <summary>True when scheduled time has passed without delivery, computed server-side.</summary>
    public bool IsOverdue { get; init; }
}
