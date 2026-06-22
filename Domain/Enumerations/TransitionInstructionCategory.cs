namespace CarePath.Domain.Enumerations;

/// <summary>
/// Clinical category of a single <see cref="Entities.Transitions.TransitionInstruction"/>
/// extracted from a discharge document. Used to route the instruction to the correct
/// reminder type and to present it in the appropriate UI section.
/// </summary>
public enum TransitionInstructionCategory
{
    /// <summary>Prescription medication — name, dose, frequency, timing, duration.</summary>
    Medication = 1,

    /// <summary>Follow-up appointment with a provider, clinic, or specialist.</summary>
    Appointment = 2,

    /// <summary>Dietary restriction or nutritional guidance.</summary>
    Diet = 3,

    /// <summary>Physical activity restriction or rehabilitation exercise.</summary>
    Activity = 4,

    /// <summary>Wound care, dressing change, or incision monitoring instruction.</summary>
    WoundCare = 5,

    /// <summary>
    /// Warning signs that require the patient to seek immediate or urgent care.
    /// Instructions in this category are never suppressed — they always appear first.
    /// </summary>
    WarningSigns = 6,

    /// <summary>Medical equipment, supply, or device requirement.</summary>
    Equipment = 7,

    /// <summary>Any instruction that does not fit a more specific category.</summary>
    Other = 8
}
