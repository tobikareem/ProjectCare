using CarePath.Contracts.Enumerations;

namespace CarePath.Web.Shared;

/// <summary>
/// One-time, in-memory navigation context for invoice-preview exclusion drill-through.
/// Client identifiers remain out of URLs and browser storage; the destination consumes and clears
/// the context immediately.
/// </summary>
public sealed class BillingReconciliationNavigationState
{
    private BillingReconciliationNavigationContext? pending;

    /// <summary>Stores the next reconciliation search context for this browser session.</summary>
    public void Set(BillingReconciliationNavigationContext context) => pending = context;

    /// <summary>Consumes the pending context once so it cannot leak into later navigation.</summary>
    public BillingReconciliationNavigationContext? Take()
    {
        var result = pending;
        pending = null;
        return result;
    }
}

/// <summary>Minimum search context needed to reproduce one preview exclusion set.</summary>
public sealed record BillingReconciliationNavigationContext(
    Guid ClientId,
    ServiceType ServiceType,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    BillingExclusionReason Reason,
    string ClientDisplayName);
