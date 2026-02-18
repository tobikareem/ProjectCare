namespace CarePath.Domain.Enumerations;

/// <summary>
/// Status of an individual payment transaction against an invoice.
/// Replaces the boolean <c>IsSuccessful</c> pattern to support the full
/// payment lifecycle including gateway-pending and refunded states.
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// Payment has been submitted but a response from the payment gateway
    /// or payer has not yet been received.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Payment has been accepted, funds confirmed, and the transaction is settled.
    /// </summary>
    Settled = 2,

    /// <summary>
    /// Payment was declined or failed (e.g. insufficient funds, chargeback).
    /// </summary>
    Failed = 3,

    /// <summary>
    /// A previously settled payment has been fully or partially reversed.
    /// </summary>
    Refunded = 4
}
