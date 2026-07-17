using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Billing;

/// <summary>
/// Dedicated audited missing-time correction (D-S6-18). Admin/Coordinator only. Records the
/// actual service window and break for a delivered shift whose times were not captured.
/// Bill-rate corrections continue through the existing guarded shift update route.
/// </summary>
public class CorrectShiftTimeRequest
{
    /// <summary>Actual service start (UTC).</summary>
    public DateTime ActualStartUtc { get; init; }

    /// <summary>Actual service end (UTC). Must be after <see cref="ActualStartUtc"/>.</summary>
    public DateTime ActualEndUtc { get; init; }

    /// <summary>Unpaid break minutes. Must be non-negative and smaller than the window.</summary>
    public int BreakMinutes { get; init; }

    /// <summary>PHI-free reason code recorded in the audit trail.</summary>
    public BillingTimeCorrectionReason Reason { get; init; }
}
