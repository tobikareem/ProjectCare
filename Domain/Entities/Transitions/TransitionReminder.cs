using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Transitions;

/// <summary>
/// A scheduled reminder or check-in prompt delivered to the patient during
/// the 30-day transition monitoring window.
/// Reminders are only created and dispatched when the parent <see cref="TransitionPlan"/>
/// has <see cref="TransitionPlanStatus.Active"/> status.
/// </summary>
public class TransitionReminder : BaseEntity
{
    /// <summary>Foreign key to the plan this reminder belongs to.</summary>
    public Guid TransitionPlanId { get; set; }

    /// <summary>Navigation to the parent <see cref="TransitionPlan"/>.</summary>
    public TransitionPlan? TransitionPlan { get; set; }

    /// <summary>
    /// The specific instruction this reminder is for, if applicable.
    /// Null for general check-in prompts not tied to a single instruction.
    /// </summary>
    public Guid? TransitionInstructionId { get; set; }

    /// <summary>The purpose of this reminder.</summary>
    public ReminderType ReminderType { get; set; }

    /// <summary>The channel through which this reminder will be (or was) delivered.</summary>
    public ReminderChannel Channel { get; set; }

    /// <summary>UTC time the reminder is scheduled to be sent.</summary>
    public DateTime ScheduledAt { get; set; }

    /// <summary>UTC time the reminder was actually dispatched to the delivery provider. Null if not yet sent.</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>UTC time the patient acknowledged the reminder. Null if not yet acknowledged.</summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>Current delivery and acknowledgement status.</summary>
    public ReminderStatus Status { get; set; } = ReminderStatus.Scheduled;

    /// <summary>
    /// Number of delivery retry attempts made after the initial send.
    /// Unacknowledged reminders are retried once before being marked <see cref="ReminderStatus.Missed"/>.
    /// </summary>
    public int RetryCount { get; set; }

    // ── Computed properties (pure C# — no EF involvement) ──────────────────────

    /// <summary>
    /// Returns <c>true</c> when the reminder is still in <see cref="ReminderStatus.Scheduled"/> status
    /// but the scheduled send time has already passed.
    /// Used by the escalation evaluator to detect delivery failures before sending.
    /// </summary>
    public bool IsOverdue => Status == ReminderStatus.Scheduled && ScheduledAt < DateTime.UtcNow;
}
