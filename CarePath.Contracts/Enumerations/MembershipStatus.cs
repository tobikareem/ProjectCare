namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.MembershipStatus</c>. Status of a platform user's membership in one organization.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum MembershipStatus
{
    /// <summary>Active.</summary>
    Active = 1,

    /// <summary>Inactive.</summary>
    Inactive = 2
}
