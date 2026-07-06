namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.ReminderStatus</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum ReminderStatus
{
    /// <summary>Scheduled; not yet sent (the only status Sprint 5 produces).</summary>
    Scheduled = 1,

    /// <summary>Sent to the patient.</summary>
    Sent = 2,

    /// <summary>Acknowledged by the patient.</summary>
    Acknowledged = 3,

    /// <summary>Not acknowledged in time.</summary>
    Missed = 4,

    /// <summary>Delivery failed.</summary>
    Failed = 5
}
