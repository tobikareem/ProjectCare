using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Billing;

/// <summary>
/// Aggregated margin metrics for one service line within a period (D-S4-2). Compensation data —
/// Admin-policy endpoints only.
/// </summary>
public class ServiceLineMarginDto
{
    /// <summary>Service line these totals cover.</summary>
    public ServiceType ServiceType { get; init; }

    /// <summary>Number of completed, billable shifts in the period.</summary>
    public int ShiftCount { get; init; }

    /// <summary>Total billable hours.</summary>
    public decimal TotalBillableHours { get; init; }

    /// <summary>Total revenue: Σ BillRate × BillableHours.</summary>
    public decimal TotalRevenue { get; init; }

    /// <summary>Total labor cost: Σ PayRate × BillableHours.</summary>
    public decimal TotalLaborCost { get; init; }

    /// <summary>Total gross margin: revenue − labor cost.</summary>
    public decimal TotalGrossMargin { get; init; }

    /// <summary>Average per-hour spread across billable hours; zero when there are no hours.</summary>
    public decimal AverageHourlyGrossMargin { get; init; }

    /// <summary>Margin as a percentage of revenue; zero when revenue is zero.</summary>
    public decimal GrossMarginPercentage { get; init; }
}
