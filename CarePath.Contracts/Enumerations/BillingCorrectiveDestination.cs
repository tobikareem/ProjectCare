namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Server-computed corrective destination for a reconciliation row (D-S6-18). Contract-only
/// presentation routing hint — the server owns the reason-to-destination rules so the UI never
/// re-derives them.
/// </summary>
public enum BillingCorrectiveDestination
{
    /// <summary>No corrective action applies (e.g., an unsuperseded non-billable resolution).</summary>
    None = 0,

    /// <summary>Navigate to the owning invoice detail (already-invoiced rows).</summary>
    InvoiceDetail = 1,

    /// <summary>Record actual times through the dedicated audited time-correction command.</summary>
    ShiftTimeCorrection = 2,

    /// <summary>Correct the bill rate through the existing guarded shift update route.</summary>
    ShiftRateUpdate = 3,

    /// <summary>Complete or correct the shift lifecycle through existing shift workflows.</summary>
    ShiftLifecycle = 4,

    /// <summary>Record an audited non-billable resolution if the service should not be billed.</summary>
    NonBillableResolution = 5,
}
