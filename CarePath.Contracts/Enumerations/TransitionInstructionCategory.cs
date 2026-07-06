namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.TransitionInstructionCategory</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum TransitionInstructionCategory
{
    /// <summary>Medication instruction.</summary>
    Medication = 1,

    /// <summary>Follow-up appointment.</summary>
    Appointment = 2,

    /// <summary>Dietary instruction.</summary>
    Diet = 3,

    /// <summary>Activity/mobility instruction.</summary>
    Activity = 4,

    /// <summary>Wound care instruction.</summary>
    WoundCare = 5,

    /// <summary>Warning signs to monitor.</summary>
    WarningSigns = 6,

    /// <summary>Medical equipment instruction.</summary>
    Equipment = 7,

    /// <summary>Other instruction.</summary>
    Other = 8
}
