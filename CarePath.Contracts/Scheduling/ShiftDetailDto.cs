using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Scheduling;

/// <summary>
/// Full shift view for Coordinator/Caregiver screens. Exposes check-in/out times but never raw
/// GPS coordinates (location verification is server-side). Excludes bill/pay rates and margins —
/// those belong to a dedicated Admin analytics contract (Sprint 4).
/// </summary>
public class ShiftDetailDto
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

    /// <summary>Actual start (UTC), once checked in.</summary>
    public DateTime? ActualStartTime { get; init; }

    /// <summary>Actual end (UTC), once checked out.</summary>
    public DateTime? ActualEndTime { get; init; }

    /// <summary>GPS check-in timestamp (UTC). Coordinates are not exposed.</summary>
    public DateTime? CheckInTime { get; init; }

    /// <summary>GPS check-out timestamp (UTC). Coordinates are not exposed.</summary>
    public DateTime? CheckOutTime { get; init; }

    /// <summary>Unpaid break minutes.</summary>
    public int BreakMinutes { get; init; }

    /// <summary>Billable hours, computed server-side from actual times and breaks.</summary>
    public decimal BillableHours { get; init; }

    /// <summary>Current shift status.</summary>
    public ShiftStatus Status { get; init; }

    /// <summary>In-home care or facility staffing.</summary>
    public ServiceType ServiceType { get; init; }

    /// <summary>Scheduling notes. May contain PHI — never log.</summary>
    public string? Notes { get; init; }

    /// <summary>Reason the shift was cancelled, when applicable. Must be PHI-free.</summary>
    public string? CancellationReason { get; init; }
}
