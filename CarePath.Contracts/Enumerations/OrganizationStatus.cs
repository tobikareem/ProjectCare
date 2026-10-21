namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.OrganizationStatus</c>. Lifecycle status of a customer organization.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum OrganizationStatus
{
    /// <summary>Pending.</summary>
    Pending = 1,

    /// <summary>Active.</summary>
    Active = 2,

    /// <summary>Suspended.</summary>
    Suspended = 3,

    /// <summary>Offboarded.</summary>
    Offboarded = 4
}
