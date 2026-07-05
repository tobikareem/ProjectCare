using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Billing;

/// <summary>
/// Request to record a payment against an invoice (invoice ID travels in the route).
/// Admin/Coordinator only. Attempted values are never echoed in validation errors (D-S4-6).
/// </summary>
public class RecordPaymentRequest
{
    /// <summary>Payment amount. Must be greater than zero.</summary>
    public decimal Amount { get; init; }

    /// <summary>Payment method.</summary>
    public PaymentMethod Method { get; init; }

    /// <summary>Payment date (UTC).</summary>
    public DateTime PaymentDate { get; init; }

    /// <summary>Processor/check reference number, when available.</summary>
    public string? ReferenceNumber { get; init; }

    /// <summary>Payment notes. Must be PHI-free.</summary>
    public string? Notes { get; init; }
}
