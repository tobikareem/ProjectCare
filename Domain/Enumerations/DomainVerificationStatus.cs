namespace CarePath.Domain.Enumerations;

/// <summary>
/// Whether a host name may resolve to its organization. Only <see cref="Verified"/> hosts resolve.
/// System-generated subdomains are verified on creation; custom domains stay pending until a
/// later verification workflow exists.
/// </summary>
public enum DomainVerificationStatus
{
    /// <summary>Registered but not yet proven to belong to the organization; never resolves.</summary>
    Pending = 1,

    /// <summary>Proven; resolves to the organization.</summary>
    Verified = 2
}
