using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Identity;

/// <summary>
/// Request to add a certification to a caregiver (caregiver ID travels in the route).
/// Admin/Coordinator only.
/// </summary>
public class AddCertificationRequest
{
    /// <summary>Certification type (CNA, RN, GNA, ...).</summary>
    public CertificationType Type { get; init; }

    /// <summary>State-issued certification number, when applicable.</summary>
    public string? CertificationNumber { get; init; }

    /// <summary>Issue date (UTC).</summary>
    public DateTime IssueDate { get; init; }

    /// <summary>Expiration date (UTC). Must be after <see cref="IssueDate"/>.</summary>
    public DateTime ExpirationDate { get; init; }

    /// <summary>Issuing authority, when recorded.</summary>
    public string? IssuingAuthority { get; init; }
}
