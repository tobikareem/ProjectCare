namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of the domain billing eligibility classification (D-S6-18). Numeric
/// values match <c>CarePath.Domain.Enumerations.BillingExclusionReason</c> exactly; ascending
/// value order is the deterministic precedence (first match wins, one reason per shift).
/// </summary>
public enum BillingExclusionReason
{
    /// <summary>A non-deleted or historical invoice line already links this shift. Informational — not revenue at risk.</summary>
    AlreadyInvoiced = 1,

    /// <summary>An authorized user recorded an unsuperseded non-billable resolution. Informational — not revenue at risk.</summary>
    NonBillableResolved = 2,

    /// <summary>The shift was cancelled or recorded as a no-show.</summary>
    CancelledOrNoShow = 3,

    /// <summary>The shift has not reached Completed status.</summary>
    NotCompleted = 4,

    /// <summary>Actual check-in or check-out time is missing.</summary>
    MissingActualTime = 5,

    /// <summary>Actual times exist but produce zero or negative billable time after breaks.</summary>
    InvalidBillableTime = 6,

    /// <summary>The shift has no positive bill rate.</summary>
    MissingBillRate = 7,

    /// <summary>No exclusion applies; the shift is billable for the period.</summary>
    Eligible = 8,
}
