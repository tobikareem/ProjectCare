using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Billing;

/// <summary>
/// Per-shift margin metrics (D-S4-2). COMPENSATION DATA: returned only by Admin-policy margin
/// endpoints — never by the normal shift contracts. Semantics per decision 0002:
/// <see cref="GrossMargin"/> is the total shift margin; <see cref="HourlyGrossMargin"/> is the
/// per-hour rate spread.
/// </summary>
public class ShiftMarginDto
{
    /// <summary>Shift identifier.</summary>
    public Guid ShiftId { get; init; }

    /// <summary>Service line of the shift.</summary>
    public ServiceType ServiceType { get; init; }

    /// <summary>Scheduled start (UTC), for period grouping.</summary>
    public DateTime ScheduledStartTime { get; init; }

    /// <summary>Billable hours, computed server-side.</summary>
    public decimal BillableHours { get; init; }

    /// <summary>Hourly bill rate.</summary>
    public decimal BillRate { get; init; }

    /// <summary>Hourly pay rate.</summary>
    public decimal PayRate { get; init; }

    /// <summary>Per-hour rate spread: BillRate − PayRate.</summary>
    public decimal HourlyGrossMargin { get; init; }

    /// <summary>Total shift margin: (BillRate − PayRate) × BillableHours.</summary>
    public decimal GrossMargin { get; init; }

    /// <summary>Margin as a percentage of revenue; zero when revenue is zero.</summary>
    public decimal GrossMarginPercentage { get; init; }
}
