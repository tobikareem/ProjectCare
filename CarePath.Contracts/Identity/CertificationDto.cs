using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Identity;

/// <summary>
/// A caregiver certification with expiry flags flattened from Domain computed properties.
/// </summary>
public class CertificationDto
{
    /// <summary>Certification record identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Owning caregiver identifier.</summary>
    public Guid CaregiverId { get; init; }

    /// <summary>Certification type (CNA, RN, GNA, ...).</summary>
    public CertificationType Type { get; init; }

    /// <summary>State-issued certification number, when applicable.</summary>
    public string? CertificationNumber { get; init; }

    /// <summary>Issue date (UTC).</summary>
    public DateTime IssueDate { get; init; }

    /// <summary>Expiration date (UTC).</summary>
    public DateTime ExpirationDate { get; init; }

    /// <summary>Issuing authority, when recorded.</summary>
    public string? IssuingAuthority { get; init; }

    /// <summary>True when the certification has expired.</summary>
    public bool IsExpired { get; init; }

    /// <summary>True when the certification expires within the alert window (30 days).</summary>
    public bool IsExpiringSoon { get; init; }
}
