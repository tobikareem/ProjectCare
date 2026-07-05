namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.UserRole</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
/// <remarks>
/// Decision D2 (Sprint 3, ratified 2026-07-04): <see cref="Clinician"/> is a first-class role
/// because plan activation is a licensure-gated action that must be distinguishable in audit logs.
/// Domain parity lands with S3-TASK-020. Family members are NOT a separate role — they authenticate
/// under <see cref="Client"/> and receive object-level access through explicit client access grants.
/// </remarks>
public enum UserRole
{
    /// <summary>Full system administrator.</summary>
    Admin = 1,

    /// <summary>Care coordinator: scheduling, matching, review queues, escalations.</summary>
    Coordinator = 2,

    /// <summary>Caregiver (W-2 employee or 1099 contractor).</summary>
    Caregiver = 3,

    /// <summary>Client (care recipient) or authorized family member acting under an explicit access grant.</summary>
    Client = 4,

    /// <summary>Facility manager for 1099 staffing placements.</summary>
    FacilityManager = 5,

    /// <summary>Licensed clinician. Reviews extracted instructions and e-signs/activates transition plans.</summary>
    Clinician = 6
}
