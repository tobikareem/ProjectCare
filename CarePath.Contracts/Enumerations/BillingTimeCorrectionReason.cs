namespace CarePath.Contracts.Enumerations;

/// <summary>
/// PHI-free reason code for the dedicated missing-time correction command (D-S6-18).
/// Recorded in audit attributes as the enum name only.
/// </summary>
public enum BillingTimeCorrectionReason
{
    /// <summary>The caregiver delivered the service but did not check out in the app.</summary>
    MissedCheckOut = 1,

    /// <summary>The caregiver could not check in/out due to a device or connectivity failure.</summary>
    DeviceFailure = 2,

    /// <summary>The recorded times were entered incorrectly and are being corrected.</summary>
    DataEntryError = 3,

    /// <summary>Another documented operational reason; details belong in the PHI-free note fields elsewhere.</summary>
    Other = 4,
}
