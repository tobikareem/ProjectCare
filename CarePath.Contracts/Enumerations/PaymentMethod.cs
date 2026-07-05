namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.PaymentMethod</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum PaymentMethod
{
    /// <summary>Cash payment.</summary>
    Cash = 1,

    /// <summary>Paper check.</summary>
    Check = 2,

    /// <summary>Credit or debit card.</summary>
    CreditCard = 3,

    /// <summary>Bank/ACH transfer.</summary>
    BankTransfer = 4,

    /// <summary>Private insurance.</summary>
    Insurance = 5,

    /// <summary>Medicaid.</summary>
    Medicaid = 6
}
