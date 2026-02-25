using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Identity;

/// <summary>
/// A single professional certification or training completion held by a caregiver.
/// Tracks type, issuing authority, issue/expiration dates, and computed expiry state.
/// </summary>
/// <remarks>
/// <para>
/// <b>Board credentials vs training completions:</b>
/// <see cref="CertificationType.CNA"/>, <see cref="CertificationType.LPN"/>,
/// <see cref="CertificationType.RN"/>, <see cref="CertificationType.HHA"/>,
/// <see cref="CertificationType.GNA"/>, and <see cref="CertificationType.CRMA"/> are issued by the
/// Maryland Board of Nursing or an accredited body and must have a <see cref="CertificationNumber"/>
/// and <see cref="IssuingAuthority"/>.
/// <see cref="CertificationType.CPR"/>, <see cref="CertificationType.FirstAid"/>,
/// <see cref="CertificationType.Dementia"/>, and <see cref="CertificationType.Alzheimers"/>
/// are training completions — <see cref="CertificationNumber"/> and <see cref="IssuingAuthority"/>
/// are optional for these types.
/// </para>
/// <para>
/// <b>Alert threshold:</b> <see cref="IsExpiringSoon"/> fires when fewer than 30 days remain.
/// Coordinators should be notified to initiate renewal before the caregiver becomes non-compliant.
/// </para>
/// </remarks>
public class CaregiverCertification : BaseEntity
{
    // Foreign Keys and Navigation

    /// <summary>Foreign key to the owning <see cref="Caregiver"/>.</summary>
    public Guid CaregiverId { get; set; }

    /// <summary>Navigation to the owning <see cref="Caregiver"/>. Required.</summary>
    public Caregiver Caregiver { get; set; } = null!;

    /// <summary>Type of certification or training completion.</summary>
    public CertificationType Type { get; set; }

    /// <summary>
    /// Credential number issued by the certifying body.
    /// Required for board credentials (CNA, LPN, RN, HHA, GNA, CRMA).
    /// Optional/not applicable for training completions (CPR, FirstAid, Dementia, Alzheimers).
    /// </summary>
    public string? CertificationNumber { get; set; }

    /// <summary>UTC date the certification was issued.</summary>
    public DateTime IssueDate { get; set; }

    /// <summary>UTC date the certification expires. Used to compute <see cref="IsExpired"/> and <see cref="IsExpiringSoon"/>.</summary>
    public DateTime ExpirationDate { get; set; }

    /// <summary>
    /// Name of the issuing authority (e.g., "Maryland Board of Nursing", "American Red Cross").
    /// Required for board credentials. Optional for training completions.
    /// </summary>
    public string? IssuingAuthority { get; set; }

    /// <summary>Number of days before expiration at which a renewal alert is triggered.</summary>
    private const int ExpirationAlertDays = 30;

    // Computed Properties

    /// <summary>
    /// <c>true</c> when <see cref="ExpirationDate"/> is in the past (relative to UTC now).
    /// An expired caregiver credential must be renewed before the caregiver is assigned to affected shifts.
    /// </summary>
    public bool IsExpired => ExpirationDate < DateTime.UtcNow;

    /// <summary>
    /// <c>true</c> when the certification is still valid but expires within
    /// <see cref="ExpirationAlertDays"/> days. Returns <c>false</c> when already expired —
    /// use <see cref="IsExpired"/> to detect past-due credentials.
    /// Triggers a coordinator alert to begin the renewal process.
    /// </summary>
    public bool IsExpiringSoon => !IsExpired && ExpirationDate < DateTime.UtcNow.AddDays(ExpirationAlertDays);
}
