namespace CarePath.Domain.Enumerations;

/// <summary>
/// Lifecycle status of a scheduled care shift.
/// </summary>
public enum ShiftStatus
{
    /// <summary>Shift is created and confirmed; caregiver has not yet started.</summary>
    Scheduled = 1,

    /// <summary>Caregiver has checked in and the shift is currently underway.</summary>
    InProgress = 2,

    /// <summary>Shift was delivered successfully and the caregiver has checked out.</summary>
    Completed = 3,

    /// <summary>Shift was cancelled before or during delivery.</summary>
    Cancelled = 4,

    /// <summary>Caregiver did not appear and did not cancel in advance.</summary>
    NoShow = 5
}
