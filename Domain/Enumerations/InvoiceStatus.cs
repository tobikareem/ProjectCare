namespace CarePath.Domain.Enumerations;

/// <summary>
/// Lifecycle status of a client invoice.
/// Lifecycle: <see cref="Draft"/> → <see cref="Sent"/> → <see cref="PartiallyPaid"/> or
/// <see cref="Paid"/> (terminal); or <see cref="Overdue"/> if payment is past due;
/// or <see cref="Cancelled"/> if voided.
/// </summary>
public enum InvoiceStatus
{
    /// <summary>Invoice is being assembled and has not been sent to the client.</summary>
    Draft = 1,

    /// <summary>Invoice has been delivered to the client and payment is awaited.</summary>
    Sent = 2,

    /// <summary>Invoice has been paid in full. Balance is zero or negative.</summary>
    Paid = 3,

    /// <summary>Invoice payment is past its due date and the full balance remains outstanding.</summary>
    Overdue = 4,

    /// <summary>Invoice has been voided and is no longer collectable.</summary>
    Cancelled = 5,

    /// <summary>
    /// One or more payments have been received but the invoice balance has not been
    /// fully settled. The outstanding balance remains collectable.
    /// Use <c>Invoice.Balance</c> to determine the exact amount outstanding.
    /// </summary>
    PartiallyPaid = 6
}
