using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Billing;

/// <summary>
/// A payment transaction recorded against an <see cref="Invoice"/>.
/// Supports partial payments, multiple payment methods, and full payment lifecycle tracking
/// (pending → settled; or failed; or refunded).
/// </summary>
/// <remarks>
/// <para>
/// <b>Partial payments:</b> Multiple <see cref="Payment"/> records may be linked to a single invoice.
/// The invoice's <c>AmountPaid</c> sums only payments with <see cref="Status"/> =
/// <see cref="PaymentStatus.Settled"/>. Pending and failed payments do not reduce the balance.
/// </para>
/// <para>
/// <b>Medicaid billing:</b> Payments with <see cref="Method"/> = <see cref="PaymentMethod.Medicaid"/>
/// follow a distinct claims and reconciliation process via the Maryland MMIS system.
/// Do not group these with private-insurance payments in financial reporting.
/// </para>
/// </remarks>
public class Payment : BaseEntity
{
    // Foreign Keys and Navigation

    /// <summary>Foreign key to the invoice this payment is applied to.</summary>
    public Guid InvoiceId { get; set; }

    /// <summary>Navigation to the parent <see cref="Invoice"/>. Required.</summary>
    public Invoice Invoice { get; set; } = null!;

    // Payment Details

    /// <summary>UTC date/time the payment was received or initiated. Defaults to the current UTC time.</summary>
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    /// <summary>Amount paid in USD. Must be greater than zero.</summary>
    public decimal Amount { get; set; }

    /// <summary>Payment method used to settle this transaction.</summary>
    public PaymentMethod Method { get; set; }

    /// <summary>
    /// External reference number for this payment (e.g., cheque number, ACH trace ID,
    /// credit card authorisation code, Medicaid remittance advice number).
    /// Optional but recommended for reconciliation.
    /// </summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>Internal notes about this payment (e.g., "Client paid two weeks late; waived fee").</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Current lifecycle status of this payment transaction.
    /// Use <see cref="PaymentStatus.Settled"/> to confirm receipt of cleared funds.
    /// </summary>
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>
    /// Reason the payment failed (e.g., "Insufficient funds", "Card declined", "Chargeback").
    /// Populated when <see cref="Status"/> transitions to <see cref="PaymentStatus.Failed"/>.
    /// </summary>
    public string? FailureReason { get; set; }
}
