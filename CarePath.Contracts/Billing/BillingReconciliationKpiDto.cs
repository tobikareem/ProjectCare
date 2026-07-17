namespace CarePath.Contracts.Billing;

/// <summary>
/// Reconciliation KPI totals computed across the ENTIRE filtered result set (D-S6-18) —
/// never just the returned page.
/// </summary>
public class BillingReconciliationKpiDto
{
    /// <summary>Unresolved service count in the filtered window.</summary>
    public int UnresolvedCount { get; init; }

    /// <summary>Sum of calculable estimated values for revenue-at-risk rows (USD).</summary>
    public decimal RevenueAtRiskValue { get; init; }

    /// <summary>Revenue-at-risk rows older than the aging threshold.</summary>
    public int AgedCount { get; init; }

    /// <summary>Sum of calculable estimated values for aged revenue-at-risk rows (USD).</summary>
    public decimal AgedValue { get; init; }
}
