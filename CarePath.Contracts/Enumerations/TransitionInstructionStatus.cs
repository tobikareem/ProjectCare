namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.TransitionInstructionStatus</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum TransitionInstructionStatus
{
    /// <summary>Awaiting clinician review. Never patient-visible.</summary>
    Pending = 1,

    /// <summary>Approved as extracted.</summary>
    Approved = 2,

    /// <summary>Rejected; excluded from the plan.</summary>
    Rejected = 3,

    /// <summary>Approved with clinician modifications.</summary>
    Modified = 4
}
