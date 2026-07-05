using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Billing;

/// <summary>
/// Full invoice view with line items and payments.
/// </summary>
public class InvoiceDetailDto
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

    /// <summary>Date fully paid (UTC), when settled.</summary>
    public DateTime? PaidDate { get; init; }

    /// <summary>Current invoice status.</summary>
    public InvoiceStatus Status { get; init; }

    /// <summary>Sum of line totals.</summary>
    public decimal Subtotal { get; init; }

    /// <summary>Tax amount.</summary>
    public decimal TaxAmount { get; init; }

    /// <summary>Invoice total (subtotal + tax).</summary>
    public decimal Total { get; init; }

    /// <summary>Sum of settled payments.</summary>
    public decimal AmountPaid { get; init; }

    /// <summary>Outstanding balance.</summary>
    public decimal Balance { get; init; }

    /// <summary>Billing notes. Must be PHI-free.</summary>
    public string? Notes { get; init; }

    /// <summary>Invoice lines.</summary>
    public IReadOnlyList<InvoiceLineItemDto> LineItems { get; init; } = [];

    /// <summary>Payments applied to this invoice.</summary>
    public IReadOnlyList<PaymentDto> Payments { get; init; } = [];
}
