namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.TransitionPlanStatus</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum TransitionPlanStatus
{
    /// <summary>Draft assembled from extraction; not yet in clinician review.</summary>
    Draft = 1,

    /// <summary>Awaiting clinician verification and e-sign.</summary>
    PendingVerification = 2,

    /// <summary>Clinician-activated; 30-day window running.</summary>
    Active = 3,

    /// <summary>Transition window completed.</summary>
    Completed = 4,

    /// <summary>Plan cancelled.</summary>
    Cancelled = 5
}
