namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.DomainVerificationStatus</c>. Whether a host name may resolve to its organization.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum DomainVerificationStatus
{
    /// <summary>Pending.</summary>
    Pending = 1,

    /// <summary>Verified.</summary>
    Verified = 2
}
