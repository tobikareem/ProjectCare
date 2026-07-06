namespace CarePath.Contracts.Billing;

/// <summary>
/// A single line on an invoice.
/// </summary>
public class InvoiceLineItemDto
{
    /// <summary>Line item identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Source shift, when the line bills a shift.</summary>
    public Guid? ShiftId { get; init; }

    /// <summary>Service description. Must be PHI-free (no clinical detail on invoices).</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Date the service was delivered (UTC).</summary>
    public DateTime ServiceDate { get; init; }

    /// <summary>Billable hours for this line.</summary>
    public decimal BillableHours { get; init; }


    /// <summary>Line total, computed server-side.</summary>
    public decimal Total { get; init; }
}
