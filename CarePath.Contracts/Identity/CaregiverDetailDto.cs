using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Identity;

/// <summary>
/// Full caregiver view for Admin/Coordinator screens. Endpoints returning this DTO must be
/// role-gated; it includes compensation data (<see cref="HourlyPayRate"/>).
/// </summary>
public class CaregiverDetailDto
{
    /// <summary>Caregiver identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Linked domain user identifier.</summary>
    public Guid UserId { get; init; }

    /// <summary>Display name ("First Last").</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Email address.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Contact phone number.</summary>
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>W-2 employee or 1099 contractor.</summary>
    public EmploymentType EmploymentType { get; init; }

    /// <summary>Hourly pay rate. Compensation data — Admin/Coordinator only.</summary>
    public decimal HourlyPayRate { get; init; }

    /// <summary>Hire date (UTC).</summary>
    public DateTime HireDate { get; init; }

    /// <summary>Termination date (UTC), when no longer active.</summary>
    public DateTime? TerminationDate { get; init; }

    /// <summary>True when trained for dementia care.</summary>
    public bool HasDementiaCare { get; init; }

    /// <summary>True when trained for Alzheimer's care.</summary>
    public bool HasAlzheimersCare { get; init; }

    /// <summary>True when trained for mobility assistance.</summary>
    public bool HasMobilityAssistance { get; init; }

    /// <summary>True when trained for medication management.</summary>
    public bool HasMedicationManagement { get; init; }

    /// <summary>Available for weekday shifts.</summary>
    public bool AvailableWeekdays { get; init; }

    /// <summary>Available for weekend shifts.</summary>
    public bool AvailableWeekends { get; init; }

    /// <summary>Available for night shifts.</summary>
    public bool AvailableNights { get; init; }

    /// <summary>Maximum scheduled hours per week.</summary>
    public int MaxWeeklyHours { get; init; }

    /// <summary>Average client rating, when available.</summary>
    public decimal? AverageRating { get; init; }

    /// <summary>Count of completed shifts.</summary>
    public int TotalShiftsCompleted { get; init; }

    /// <summary>Count of no-shows.</summary>
    public int NoShowCount { get; init; }

    /// <summary>The caregiver's certifications.</summary>
    public IReadOnlyList<CertificationDto> Certifications { get; init; } = [];
}
