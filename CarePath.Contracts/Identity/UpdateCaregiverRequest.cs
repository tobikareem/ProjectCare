namespace CarePath.Contracts.Identity;

/// <summary>
/// Request to update a caregiver profile. Admin/Coordinator only. Email and name changes are
/// Identity-account operations and are not part of this request.
/// </summary>
public class UpdateCaregiverRequest
{
    /// <summary>Contact phone number.</summary>
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>Hourly pay rate.</summary>
    public decimal HourlyPayRate { get; init; }

    /// <summary>Termination date (UTC); set when off-boarding.</summary>
    public DateTime? TerminationDate { get; init; }

    /// <summary>Trained for dementia care.</summary>
    public bool HasDementiaCare { get; init; }

    /// <summary>Trained for Alzheimer's care.</summary>
    public bool HasAlzheimersCare { get; init; }

    /// <summary>Trained for mobility assistance.</summary>
    public bool HasMobilityAssistance { get; init; }

    /// <summary>Trained for medication management.</summary>
    public bool HasMedicationManagement { get; init; }

    /// <summary>Available for weekday shifts.</summary>
    public bool AvailableWeekdays { get; init; }

    /// <summary>Available for weekend shifts.</summary>
    public bool AvailableWeekends { get; init; }

    /// <summary>Available for night shifts.</summary>
    public bool AvailableNights { get; init; }

    /// <summary>Maximum scheduled hours per week.</summary>
    public int MaxWeeklyHours { get; init; }
}
