namespace CarePath.Contracts.Billing;

/// <summary>
/// Server-paged reconciliation search result (D-S6-18): one page of rows ordered oldest
/// service date then Shift ID, plus full-filter KPI totals.
/// </summary>
public class BillingReconciliationSearchResponseDto
{
    /// <summary>The requested page of reconciliation rows.</summary>
    public IReadOnlyList<BillingReconciliationRowDto> Rows { get; init; } = [];

    /// <summary>One-based page number echoed from the request.</summary>
    public int PageNumber { get; init; }

    /// <summary>Page size echoed from the request (after clamping).</summary>
    public int PageSize { get; init; }

    /// <summary>Total matching rows across the full filter.</summary>
    public int TotalCount { get; init; }

    /// <summary>KPI totals across the full filter (not just this page).</summary>
    public BillingReconciliationKpiDto Kpis { get; init; } = new();
}
