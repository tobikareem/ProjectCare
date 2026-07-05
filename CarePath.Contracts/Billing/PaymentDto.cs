using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Billing;

/// <summary>
/// A payment applied to an invoice.
/// </summary>
public class PaymentDto
{
    /// <summary>Payment identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Invoice the payment applies to.</summary>
    public Guid InvoiceId { get; init; }

    /// <summary>Payment date (UTC).</summary>
    public DateTime PaymentDate { get; init; }

    /// <summary>Payment amount.</summary>
    public decimal Amount { get; init; }

    /// <summary>Payment method.</summary>
    public PaymentMethod Method { get; init; }

    /// <summary>Current payment status.</summary>
    public PaymentStatus Status { get; init; }

    /// <summary>Processor/check reference number, when available.</summary>
    public string? ReferenceNumber { get; init; }

    /// <summary>Failure description for failed payments. Must be PHI-free.</summary>
    public string? FailureReason { get; init; }
}
