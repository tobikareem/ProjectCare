using CarePath.Domain.Enumerations;

namespace CarePath.Application.Abstractions.Billing;

/// <summary>
/// Deterministic caregiver qualification labeling for billing surfaces (D-S6-18). Only board
/// credentials count (RN, LPN, GNA, CNA, HHA, CRMA — in that canonical display order);
/// training completions and credential numbers are never included. A caregiver with no
/// credential valid on the service date renders <see cref="NoCredentialLabel"/>.
/// </summary>
public static class BillingQualification
{
    /// <summary>Label used when no professional credential is valid on the service date.</summary>
    public const string NoCredentialLabel = "Caregiver";

    private static readonly CertificationType[] CanonicalOrder =
    [
        CertificationType.RN,
        CertificationType.LPN,
        CertificationType.GNA,
        CertificationType.CNA,
        CertificationType.HHA,
        CertificationType.CRMA,
    ];

    /// <summary>
    /// Builds the label from the caregiver's certifications, keeping only professional
    /// credentials whose validity window covers the service date, deduplicated and ordered
    /// canonically (e.g., <c>"RN, CNA"</c>).
    /// </summary>
    /// <param name="certifications">Certification type + issue/expiration dates.</param>
    /// <param name="serviceDateUtc">The service date (UTC).</param>
    public static string LabelFor(
        IEnumerable<(CertificationType Type, DateTime IssueDate, DateTime ExpirationDate)> certifications,
        DateTime serviceDateUtc)
    {
        var serviceDate = serviceDateUtc.Date;
        var valid = certifications
            .Where(certification => CanonicalOrder.Contains(certification.Type)
                && certification.IssueDate.Date <= serviceDate
                && certification.ExpirationDate.Date >= serviceDate)
            .Select(certification => certification.Type)
            .Distinct()
            .OrderBy(type => Array.IndexOf(CanonicalOrder, type))
            .ToArray();

        return valid.Length == 0
            ? NoCredentialLabel
            : string.Join(", ", valid.Select(type => type.ToString()));
    }
}
