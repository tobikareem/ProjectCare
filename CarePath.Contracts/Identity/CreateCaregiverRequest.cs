using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Identity;

/// <summary>
/// Request to create a caregiver: Domain user + caregiver profile + Identity account in one
/// workflow (D-S4-5). Admin/Coordinator only.
/// </summary>
/// <remarks>
/// CREDENTIAL SAFETY (D-S4-5): <see cref="TemporaryPassword"/> is one-time provisioning
/// material — it must never be committed, logged, echoed in validation errors, or returned in
/// any response. Request-body logging is disabled on provisioning routes.
/// </remarks>
public class CreateCaregiverRequest
{
    /// <summary>First name.</summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>Last name.</summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>Unique email address (becomes the Identity login).</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Contact phone number.</summary>
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>One-time temporary password for the new Identity account. Never logged or echoed.</summary>
    public string? TemporaryPassword { get; init; }

    /// <summary>W-2 employee or 1099 contractor.</summary>
    public EmploymentType EmploymentType { get; init; }

    /// <summary>Hourly pay rate.</summary>
    public decimal HourlyPayRate { get; init; }

    /// <summary>Hire date (UTC).</summary>
    public DateTime HireDate { get; init; }

    /// <summary>Trained for dementia care.</summary>
    public bool HasDementiaCare { get; init; }

    /// <summary>Trained for Alzheimer's care.</summary>
    public bool HasAlzheimersCare { get; init; }

    /// <summary>Trained for mobility assistance.</summary>
    public bool HasMobilityAssistance { get; init; }

    /// <summary>Trained for medication management.</summary>
    public bool HasMedicationManagement { get; init; }

    /// <summary>Available for weekday shifts.</summary>
    public bool AvailableWeekdays { get; init; } = true;

    /// <summary>Available for weekend shifts.</summary>
    public bool AvailableWeekends { get; init; }

    /// <summary>Available for night shifts.</summary>
    public bool AvailableNights { get; init; }

    /// <summary>Maximum scheduled hours per week.</summary>
    public int MaxWeeklyHours { get; init; } = 40;
}
