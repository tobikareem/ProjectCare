namespace CarePath.Domain.Enumerations;

/// <summary>
/// PHI-free reason recorded on an append-only billing reconciliation resolution (D-S6-18).
/// Values describe operational billing decisions only — never clinical context.
/// </summary>
public enum BillingReconciliationReason
{
    /// <summary>The shift record was created in error or duplicates another record.</summary>
    DataEntryError = 1,

    /// <summary>The service is intentionally not billed as a goodwill or courtesy service.</summary>
    GoodwillService = 2,

    /// <summary>The shift was a training, orientation, or shadowing engagement.</summary>
    TrainingShift = 3,

    /// <summary>The service is excluded from billing under the client's contract terms.</summary>
    ContractExclusion = 4,

    /// <summary>Another documented operational reason; details belong in the PHI-free note.</summary>
    Other = 5,

    /// <summary>
    /// Appended when an authorized user reopens a previously resolved shift. A reopen record
    /// supersedes the prior resolution and returns the shift to the unresolved queue.
    /// </summary>
    Reopened = 6,
}
