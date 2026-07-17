namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of the domain reconciliation resolution reason (D-S6-18). Numeric values
/// match <c>CarePath.Domain.Enumerations.BillingReconciliationReason</c> exactly. Reasons are
/// operational billing decisions only and must never encode clinical context.
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

    /// <summary>Reserved for reopen records; rejected on resolve requests.</summary>
    Reopened = 6,
}
