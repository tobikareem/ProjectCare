using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Platform;

/// <summary>
/// A customer agency (tenant) registered in the control plane. Holds no clinical data.
/// </summary>
/// <remarks>
/// <see cref="Slug"/> is immutable after creation and unique platform-wide. Tenant traffic is
/// admitted only while <see cref="IsAdmissible"/> is true (ADR 0003 §8.2, D-CP04-03).
/// </remarks>
public class Organization : BaseEntity
{
    /// <summary>Registered legal name (max 200).</summary>
    public string LegalName { get; set; } = string.Empty;

    /// <summary>Name shown in the UI and login page (max 100).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Immutable URL-safe identifier: 3–40 characters, lower-case letters, digits, and single
    /// hyphens; unique. Reserved values (platform, admin, api, www, login) are refused.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Lifecycle status.</summary>
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Pending;

    /// <summary>Data-plane provisioning state.</summary>
    public ProvisioningState ProvisioningState { get; set; } = ProvisioningState.Registered;

    /// <summary>IANA time zone identifier (max 64), e.g. <c>America/New_York</c>.</summary>
    public string DefaultTimeZone { get; set; } = "America/New_York";

    /// <summary>Deployment region label (max 32).</summary>
    public string DataRegion { get; set; } = "us-east";

    /// <summary>Operator-entered reason recorded when suspended (max 200). Never PHI.</summary>
    public string? SuspensionReason { get; set; }

    /// <summary>UTC timestamp of the most recent suspension; <c>null</c> when never suspended.</summary>
    public DateTime? SuspendedAtUtc { get; set; }

    /// <summary>
    /// True when tenant traffic may be admitted: <see cref="OrganizationStatus.Active"/> and
    /// <see cref="Enumerations.ProvisioningState.Provisioned"/>.
    /// </summary>
    public bool IsAdmissible =>
        Status == OrganizationStatus.Active && ProvisioningState == ProvisioningState.Provisioned;
}
