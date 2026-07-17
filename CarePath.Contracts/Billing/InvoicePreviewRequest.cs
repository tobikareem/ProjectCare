using CarePath.Contracts.Common;
using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Billing;

/// <summary>
/// Body-based invoice preview request (D-S6-18). Admin/Coordinator only. Selection fields plus
/// paging only — the server derives everything else and remains authoritative for eligibility.
/// </summary>
public class InvoicePreviewRequest : PagedRequest
{
    /// <summary>Client (or facility billing account) to preview.</summary>
    public Guid ClientId { get; init; }

    /// <summary>Service line the invoice would cover.</summary>
    public ServiceType ServiceType { get; init; }

    /// <summary>Billing period start (UTC, inclusive).</summary>
    public DateTime PeriodStartUtc { get; init; }

    /// <summary>Billing period end (UTC, exclusive). Must be after <see cref="PeriodStartUtc"/>.</summary>
    public DateTime PeriodEndUtc { get; init; }
}
