namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.EscalationTriggerType</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum EscalationTriggerType
{
    /// <summary>A critical task/reminder was missed.</summary>
    MissedCriticalTask = 1,

    /// <summary>Patient reported warning symptoms.</summary>
    WarningSymptomsReported = 2,

    /// <summary>Patient could not be reached.</summary>
    FailedContact = 3,

    /// <summary>Caregiver raised an alert.</summary>
    CaregiverAlert = 4
}
