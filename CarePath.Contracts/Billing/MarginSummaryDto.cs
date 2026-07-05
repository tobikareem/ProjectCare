namespace CarePath.Contracts.Billing;

/// <summary>
/// Period margin summary split by service line (D-S4-2). Compensation data — returned only by
/// the Admin-policy <c>/api/billing/margins</c> endpoint.
/// </summary>
public class MarginSummaryDto
{
    /// <summary>Period start (UTC, inclusive).</summary>
    public DateTime PeriodStartUtc { get; init; }

    /// <summary>Period end (UTC, exclusive).</summary>
    public DateTime PeriodEndUtc { get; init; }

    /// <summary>In-home care service line totals.</summary>
    public ServiceLineMarginDto InHomeCare { get; init; } = new();

    /// <summary>Facility staffing service line totals.</summary>
    public ServiceLineMarginDto FacilityStaffing { get; init; } = new();

    /// <summary>Combined totals across both service lines.</summary>
    public ServiceLineMarginDto Overall { get; init; } = new();
}
