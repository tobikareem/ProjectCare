namespace CarePath.Domain.Enumerations;

/// <summary>
/// Billing eligibility classification for a shift within a billing period (D-S6-18).
/// Exactly one value applies per shift, evaluated in ascending numeric precedence:
/// the first matching rule wins and later rules are not considered.
/// </summary>
/// <remarks>
/// <see cref="AlreadyInvoiced"/> and <see cref="NonBillableResolved"/> are informational
/// classifications and are never counted as revenue at risk. <see cref="Eligible"/> is the
/// terminal classification when no exclusion applies.
/// </remarks>
public enum BillingExclusionReason
{
    /// <summary>A non-deleted or historical invoice line already links this shift.</summary>
    AlreadyInvoiced = 1,

    /// <summary>An authorized user recorded an unsuperseded non-billable resolution.</summary>
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
