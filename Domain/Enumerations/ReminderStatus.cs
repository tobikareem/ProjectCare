namespace CarePath.Domain.Enumerations;

/// <summary>
/// Delivery and acknowledgement lifecycle of a <see cref="Entities.Transitions.TransitionReminder"/>.
/// </summary>
public enum ReminderStatus
{
    /// <summary>Reminder is queued and has not yet been sent.</summary>
    Scheduled = 1,

    /// <summary>Reminder was dispatched to the delivery provider (Twilio or push).</summary>
    Sent = 2,

    /// <summary>Patient confirmed receipt or completion (tap, SMS reply, or IVR keypress).</summary>
    Acknowledged = 3,

    /// <summary>
    /// Reminder was sent but not acknowledged within the retry window.
    /// Triggers escalation evaluation based on the plan's <see cref="TransitionRiskLevel"/>.
    /// </summary>
    Missed = 4,

    /// <summary>Delivery failed at the provider level (invalid number, network error, etc.).</summary>
    Failed = 5
}
