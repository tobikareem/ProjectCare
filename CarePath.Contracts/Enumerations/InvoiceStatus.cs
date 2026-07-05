namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.InvoiceStatus</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum InvoiceStatus
{
    /// <summary>Invoice created but not yet sent.</summary>
    Draft = 1,

    /// <summary>Invoice sent to the payer.</summary>
    Sent = 2,

    /// <summary>Invoice fully paid.</summary>
    Paid = 3,

    /// <summary>Invoice past its due date with a balance outstanding.</summary>
    Overdue = 4,

    /// <summary>Invoice cancelled.</summary>
    Cancelled = 5,

    /// <summary>Invoice partially paid; balance remains.</summary>
    PartiallyPaid = 6
}
