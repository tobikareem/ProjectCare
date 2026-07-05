using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Billing;

/// <summary>
/// Request to create an invoice for a client's completed shifts in a billing period (D-S4-6).
/// Idempotent: repeating the same client/service-line/period returns a PHI-free conflict.
/// Admin/Coordinator only.
/// </summary>
public class CreateInvoiceRequest
{
    /// <summary>Client to invoice.</summary>
    public Guid ClientId { get; init; }

    /// <summary>Service line the invoice covers.</summary>
    public ServiceType ServiceType { get; init; }

    /// <summary>Billing period start (UTC, inclusive).</summary>
    public DateTime PeriodStartUtc { get; init; }

    /// <summary>Billing period end (UTC, exclusive). Must be after <see cref="PeriodStartUtc"/>.</summary>
    public DateTime PeriodEndUtc { get; init; }

    /// <summary>Due date (UTC).</summary>
    public DateTime DueDate { get; init; }

    /// <summary>Tax amount, when applicable.</summary>
    public decimal TaxAmount { get; init; }

    /// <summary>Billing notes. Must be PHI-free.</summary>
    public string? Notes { get; init; }
}
