using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Identity;

/// <summary>
/// Caregiver row for scheduling and matching lists. Excludes pay rates — those appear only
/// on <see cref="CaregiverDetailDto"/> behind Admin/Coordinator authorization.
/// </summary>
public class CaregiverSummaryDto
{
    /// <summary>Caregiver identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Linked domain user identifier.</summary>
    public Guid UserId { get; init; }

    /// <summary>Display name ("First Last").</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>W-2 employee or 1099 contractor.</summary>
    public EmploymentType EmploymentType { get; init; }

    /// <summary>Average client rating, when available.</summary>
    public decimal? AverageRating { get; init; }

    /// <summary>False when the caregiver's user account is deactivated.</summary>
    public bool IsActive { get; init; }
}
