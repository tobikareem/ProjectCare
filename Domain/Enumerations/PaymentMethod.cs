namespace CarePath.Domain.Enumerations;

/// <summary>
/// Payment method used to settle an invoice.
/// </summary>
public enum PaymentMethod
{
    /// <summary>Physical cash payment received in person.</summary>
    Cash = 1,

    /// <summary>Personal or business cheque.</summary>
    Check = 2,

    /// <summary>Credit or debit card transaction.</summary>
    CreditCard = 3,

    /// <summary>ACH or wire bank transfer.</summary>
    BankTransfer = 4,

    /// <summary>
    /// Payment received from a private insurance carrier (e.g., Blue Cross, Aetna, UnitedHealth).
    /// Claims submitted via the carrier's clearinghouse or payer portal.
    /// </summary>
    Insurance = 5,

    /// <summary>
    /// Payment received from Maryland Medicaid (Medical Assistance Program).
    /// Claims submitted to the Maryland MMIS system. Distinct billing and reconciliation
    /// process from private insurance — do not group with <see cref="Insurance"/>.
    /// </summary>
    Medicaid = 6
}
