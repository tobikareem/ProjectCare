namespace CarePath.Domain.Enumerations;

/// <summary>
/// Tracks the processing lifecycle of a <see cref="CarePath.Domain.Entities.Transitions.DischargeDocument"/>.
/// </summary>
public enum DischargeDocumentStatus
{
    /// <summary>Document has been received but AI extraction has not yet started.</summary>
    Pending = 1,

    /// <summary>AI extraction is actively running as a background job.</summary>
    Extracting = 2,

    /// <summary>Extraction is complete. A draft TransitionPlan is awaiting clinician review.</summary>
    AwaitingReview = 3,

    /// <summary>A clinician has reviewed and approved the extracted plan. TransitionPlan is active.</summary>
    Approved = 4,

    /// <summary>A clinician rejected the extracted plan. Manual re-upload or re-extraction required.</summary>
    Rejected = 5
}
