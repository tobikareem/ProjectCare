namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.AccessScope</c> (lands with S4-TASK-011).
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// Scope of a family-proxy client access grant per Sprint 4 decision D-S4-1.
/// </summary>
public enum AccessScope
{
    /// <summary>
    /// Read access to explicitly patient-facing views only — never operational staff fields,
    /// rates, margins, raw care-plan clinical text, or internal notes.
    /// </summary>
    PatientFacing = 1,

    /// <summary>
    /// Access to the PHI-bearing client records that each endpoint explicitly allows for
    /// Client-role users.
    /// </summary>
    Full = 2
}
