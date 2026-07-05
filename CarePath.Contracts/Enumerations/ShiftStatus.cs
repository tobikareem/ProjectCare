namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.ShiftStatus</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum ShiftStatus
{
    /// <summary>Shift is scheduled but not yet started.</summary>
    Scheduled = 1,

    /// <summary>Caregiver has checked in; shift is underway.</summary>
    InProgress = 2,

    /// <summary>Shift finished and checked out.</summary>
    Completed = 3,

    /// <summary>Shift was cancelled before completion.</summary>
    Cancelled = 4,

    /// <summary>Caregiver did not arrive for the shift.</summary>
    NoShow = 5
}
