namespace CarePath.Contracts.Billing;

/// <summary>
/// Invoice preview result (D-S6-18): one page of eligible rows plus aggregates computed across
/// the ENTIRE matching set, exclusion counts, and an opaque expiring preview token that
/// <c>CreateInvoiceRequest</c> must echo back to generate the invoice.
/// </summary>
public class InvoicePreviewResponseDto
{
    /// <summary>The requested page of eligible billable rows (stable service-date order).</summary>
    public IReadOnlyList<InvoicePreviewRowDto> Rows { get; init; } = [];

    /// <summary>One-based page number echoed from the request.</summary>
    public int PageNumber { get; init; }

    /// <summary>Page size echoed from the request (after clamping).</summary>
    public int PageSize { get; init; }

    /// <summary>Total eligible shifts across the full period (not just this page).</summary>
    public int EligibleShiftCount { get; init; }

    /// <summary>Total billable hours across all eligible shifts.</summary>
    public decimal TotalBillableHours { get; init; }

    /// <summary>Sum of all rounded eligible line totals (USD).</summary>
    public decimal Subtotal { get; init; }

    /// <summary>Per-reason exclusion counts across the full period. Informational.</summary>
    public IReadOnlyList<InvoiceExclusionCountDto> ExclusionCounts { get; init; } = [];

    /// <summary>
    /// Opaque, tamper-protected preview token bound to the selection, eligible shifts, billable
    /// inputs, and totals. Contents are never interpretable client-side and must never be
    /// logged or stored beyond the generate flow.
    /// </summary>
    public string PreviewToken { get; init; } = string.Empty;

    /// <summary>UTC expiry of <see cref="PreviewToken"/>; a later create returns <c>invoice.preview_stale</c>.</summary>
    public DateTime PreviewTokenExpiresAtUtc { get; init; }
}
