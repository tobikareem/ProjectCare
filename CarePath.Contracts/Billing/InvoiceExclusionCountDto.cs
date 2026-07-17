using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Billing;

/// <summary>
/// Count of shifts excluded from an invoice preview for one reason (D-S6-18). Links to the
/// separately authorized reconciliation review — the preview itself never carries excluded-row
/// detail.
/// </summary>
public class InvoiceExclusionCountDto
{
    /// <summary>The exclusion reason. Never <see cref="BillingExclusionReason.Eligible"/>.</summary>
    public BillingExclusionReason Reason { get; init; }

    /// <summary>Number of shifts excluded for this reason across the full period.</summary>
    public int Count { get; init; }
}
