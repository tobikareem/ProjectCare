using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Billing;

/// <summary>
/// Invoice row for billing lists. Monetary totals are computed server-side.
/// </summary>
public class InvoiceSummaryDto
{
    /// <summary>Invoice identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Human-readable invoice number.</summary>
    public string InvoiceNumber { get; init; } = string.Empty;

    /// <summary>Billed client identifier.</summary>
    public Guid ClientId { get; init; }

    /// <summary>Billed client display name.</summary>
    public string ClientFullName { get; init; } = string.Empty;

    /// <summary>Invoice date (UTC).</summary>
    public DateTime InvoiceDate { get; init; }

    /// <summary>Due date (UTC).</summary>
    public DateTime DueDate { get; init; }

    /// <summary>Current invoice status.</summary>
    public InvoiceStatus Status { get; init; }

    /// <summary>Invoice total (subtotal + tax).</summary>
    public decimal Total { get; init; }

    /// <summary>Sum of settled payments.</summary>
    public decimal AmountPaid { get; init; }

    /// <summary>Outstanding balance.</summary>
    public decimal Balance { get; init; }
}
