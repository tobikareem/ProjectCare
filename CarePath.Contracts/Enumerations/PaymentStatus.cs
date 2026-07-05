namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.PaymentStatus</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum PaymentStatus
{
    /// <summary>Payment initiated but not yet settled.</summary>
    Pending = 1,

    /// <summary>Payment settled successfully.</summary>
    Settled = 2,

    /// <summary>Payment failed.</summary>
    Failed = 3,

    /// <summary>Payment refunded.</summary>
    Refunded = 4
}
