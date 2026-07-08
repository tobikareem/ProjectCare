using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Scheduling;

/// <summary>
/// Candidate caregiver row for shift assignment. Includes no compensation fields.
/// </summary>
public class EligibleCaregiverDto
{
    /// <summary>Caregiver identifier.</summary>
    public Guid CaregiverId { get; init; }

    /// <summary>Caregiver display name.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>W-2 employee or 1099 contractor.</summary>
    public EmploymentType EmploymentType { get; init; }

    /// <summary>Average rating, when available.</summary>
    public decimal? AverageRating { get; init; }

    /// <summary>Current-month completed shifts derived from check-in/out records.</summary>
    public int ShiftsMtd { get; init; }

    /// <summary>True when assignment may proceed through the shift update endpoint.</summary>
    public bool IsAssignable { get; init; }

    /// <summary>Positive match reasons such as credential and availability fit.</summary>
    public IReadOnlyList<string> MatchReasons { get; init; } = [];

    /// <summary>Blocking reasons such as expired credentials or double-booking.</summary>
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];
}
