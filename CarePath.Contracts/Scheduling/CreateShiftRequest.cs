using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Scheduling;

/// <summary>
/// Request to schedule a shift. Field-for-field aligned with the Application layer's
/// <c>CreateShiftCommand</c>; validated by FluentValidation at the Application boundary
/// (end must be after start, rates positive, break non-negative).
/// </summary>
public class CreateShiftRequest
{
    /// <summary>Client to serve.</summary>
    public Guid ClientId { get; init; }

    /// <summary>
    /// Caregiver to assign, or <c>null</c> to create an open (unassigned) shift that enters
    /// the coverage queue for later assignment (D-S6-12). Eligibility guards (double-booking,
    /// credentials) run only when a caregiver is supplied.
    /// </summary>
    public Guid? CaregiverId { get; init; }

    /// <summary>Scheduled start (UTC).</summary>
    public DateTime ScheduledStartUtc { get; init; }

    /// <summary>Scheduled end (UTC). Must be after <see cref="ScheduledStartUtc"/>.</summary>
    public DateTime ScheduledEndUtc { get; init; }

    /// <summary>Unpaid break minutes, when known at scheduling time.</summary>
    public int? BreakMinutes { get; init; }

    /// <summary>Hourly bill rate for this shift.</summary>
    public decimal BillRate { get; init; }

    /// <summary>Hourly pay rate for this shift.</summary>
    public decimal PayRate { get; init; }

    /// <summary>In-home care or facility staffing.</summary>
    public ServiceType ServiceType { get; init; }
}
