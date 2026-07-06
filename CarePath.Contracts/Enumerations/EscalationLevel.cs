namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.EscalationLevel</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
/// <remarks>
/// SAFETY (D-S5-7): the system only ever creates <see cref="CoordinatorAlert"/> records.
/// The other levels represent human decisions documented by a coordinator during
/// acknowledgement — never automated actions.
/// </remarks>
public enum EscalationLevel
{
    /// <summary>Alert on the coordinator dashboard (the only system-created level).</summary>
    CoordinatorAlert = 1,

    /// <summary>Coordinator decided to notify family.</summary>
    FamilyNotification = 2,

    /// <summary>Coordinator recommended urgent care.</summary>
    UrgentCare = 3,

    /// <summary>Coordinator escalated to emergency services.</summary>
    Emergency911 = 4
}
