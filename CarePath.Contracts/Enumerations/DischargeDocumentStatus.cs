namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.DischargeDocumentStatus</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum DischargeDocumentStatus
{
    /// <summary>Received; extraction not yet started.</summary>
    Pending = 1,

    /// <summary>Extraction in progress.</summary>
    Extracting = 2,

    /// <summary>Draft instructions ready for clinician review.</summary>
    AwaitingReview = 3,

    /// <summary>Clinician approved the extracted content.</summary>
    Approved = 4,

    /// <summary>Clinician rejected the extracted content.</summary>
    Rejected = 5
}
