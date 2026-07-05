using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Scheduling;

/// <summary>
/// Shift row for schedule lists and calendars. Excludes rates and margin data (Admin
/// dashboards get a dedicated, role-gated contract in Sprint 4) and all GPS data.
/// </summary>
public class ShiftSummaryDto
{
    /// <summary>Shift identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Client identifier.</summary>
    public Guid ClientId { get; init; }

    /// <summary>Client display name.</summary>
    public string ClientFullName { get; init; } = string.Empty;

    /// <summary>Assigned caregiver identifier, when assigned.</summary>
    public Guid? CaregiverId { get; init; }

    /// <summary>Assigned caregiver display name, when assigned.</summary>
    public string? CaregiverFullName { get; init; }

    /// <summary>Scheduled start (UTC).</summary>
    public DateTime ScheduledStartTime { get; init; }

    /// <summary>Scheduled end (UTC).</summary>
    public DateTime ScheduledEndTime { get; init; }

    /// <summary>Current shift status.</summary>
    public ShiftStatus Status { get; init; }

    /// <summary>In-home care or facility staffing.</summary>
    public ServiceType ServiceType { get; init; }
}
