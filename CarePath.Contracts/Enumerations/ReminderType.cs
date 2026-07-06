namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.ReminderType</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum ReminderType
{
    /// <summary>Medication reminder.</summary>
    Medication = 1,

    /// <summary>Appointment reminder.</summary>
    Appointment = 2,

    /// <summary>Symptom check-in prompt.</summary>
    SymptomCheckIn = 3,

    /// <summary>Prescription refill reminder.</summary>
    Refill = 4,

    /// <summary>Equipment reminder.</summary>
    Equipment = 5,

    /// <summary>Diet reminder.</summary>
    Diet = 6,

    /// <summary>Activity reminder.</summary>
    Activity = 7
}
