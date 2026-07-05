using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Scheduling;

/// <summary>
/// Request to update a scheduled shift (shift ID travels in the route). Admin/Coordinator only.
/// Reassignment and rescheduling pass through the double-booking and certification guards
/// (D-S4-4). Cancellation is a separate command, not this request.
/// </summary>
public class UpdateShiftRequest
{
    /// <summary>Caregiver to assign, or <c>null</c> to leave the shift unassigned.</summary>
    public Guid? CaregiverId { get; init; }

    /// <summary>Scheduled start (UTC).</summary>
    public DateTime ScheduledStartUtc { get; init; }

    /// <summary>Scheduled end (UTC). Must be after <see cref="ScheduledStartUtc"/>.</summary>
    public DateTime ScheduledEndUtc { get; init; }

    /// <summary>Unpaid break minutes, when known.</summary>
    public int? BreakMinutes { get; init; }

    /// <summary>Hourly bill rate for this shift.</summary>
    public decimal BillRate { get; init; }

    /// <summary>Hourly pay rate for this shift.</summary>
    public decimal PayRate { get; init; }

    /// <summary>In-home care or facility staffing.</summary>
    public ServiceType ServiceType { get; init; }

    /// <summary>Scheduling notes. May contain PHI — never log.</summary>
    public string? Notes { get; init; }
}
