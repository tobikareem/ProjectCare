namespace CarePath.Domain.Enumerations;

/// <summary>
/// The purpose of a <see cref="Entities.Transitions.TransitionReminder"/> sent to the patient.
/// </summary>
public enum ReminderType
{
    /// <summary>Reminder to take a prescribed medication at the scheduled time.</summary>
    Medication = 1,

    /// <summary>Reminder about an upcoming follow-up appointment.</summary>
    Appointment = 2,

    /// <summary>Prompt for the patient to complete a symptom and adherence check-in.</summary>
    SymptomCheckIn = 3,

    /// <summary>Alert that a prescription supply is running low and needs to be refilled.</summary>
    Refill = 4,

    /// <summary>Reminder to use, clean, or check a prescribed medical device or supply.</summary>
    Equipment = 5,

    /// <summary>Reminder about a diet restriction or nutritional instruction.</summary>
    Diet = 6,

    /// <summary>Reminder about a physical activity restriction or prescribed exercise.</summary>
    Activity = 7
}
