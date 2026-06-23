namespace CarePath.Domain.Enumerations;

/// <summary>
/// Clinical risk stratification for a <see cref="Entities.Transitions.TransitionPlan"/>.
/// Controls reminder frequency and escalation response times.
/// </summary>
public enum TransitionRiskLevel
{
    /// <summary>
    /// Low risk. Check-ins on Days 1–3, 7, 14, 21, 30.
    /// Missed reminders escalate within 24 hours.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Medium risk (default). Check-ins on Days 1–7 then every 2 days.
    /// Missed reminders escalate within 8 hours.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// High risk. Daily check-ins throughout the 30-day window.
    /// All missed reminders escalate within 4 hours.
    /// </summary>
    High = 3
}
