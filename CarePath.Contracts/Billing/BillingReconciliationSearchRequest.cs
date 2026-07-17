using CarePath.Contracts.Common;
using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Billing;

/// <summary>
/// Body-based reconciliation search (D-S6-18). Admin/Coordinator only. The service-date window
/// is half-open UTC and may span at most 92 days; results are server-paged and ordered oldest
/// service date first, then Shift ID.
/// </summary>
public class BillingReconciliationSearchRequest : PagedRequest
{
    /// <summary>Maximum allowed service-date window in days.</summary>
    public const int MaxRangeDays = 92;

    /// <summary>Service-date window start (UTC, inclusive).</summary>
    public DateTime PeriodStartUtc { get; init; }

    /// <summary>Service-date window end (UTC, exclusive). Must be after <see cref="PeriodStartUtc"/>.</summary>
    public DateTime PeriodEndUtc { get; init; }

    /// <summary>Optional client (or facility billing account) filter.</summary>
    public Guid? ClientId { get; init; }

    /// <summary>Optional service-line filter.</summary>
    public ServiceType? ServiceType { get; init; }

    /// <summary>Optional single-reason filter.</summary>
    public BillingExclusionReason? Reason { get; init; }

    /// <summary>When true, returns only aged revenue-at-risk rows (see D-S6-18 24-hour rule).</summary>
    public bool AgedRiskOnly { get; init; }
}
