namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.ReminderChannel</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum ReminderChannel
{
    /// <summary>In-app notification.</summary>
    App = 1,

    /// <summary>SMS text message (delivery lands in Sprint 7 behind Twilio gates).</summary>
    Sms = 2,

    /// <summary>Voice call (delivery lands in Sprint 7 behind Twilio gates).</summary>
    Voice = 3
}
