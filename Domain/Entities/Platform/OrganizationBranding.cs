using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Platform;

/// <summary>
/// White-label presentation fields for one organization. Colors are design-system tokens,
/// never raw values; no free-form CSS, HTML, or script is ever stored. One row per organization.
/// </summary>
public class OrganizationBranding : BaseEntity
{
    /// <summary>Owning organization (unique).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Navigation to the owning organization.</summary>
    public Organization? Organization { get; set; }

    /// <summary>
    /// Private storage key of the logo blob (max 256). Resolved to a short-lived URL by a later
    /// spec; never a public URL and never returned on the anonymous branding endpoint.
    /// </summary>
    public string? LogoStorageKey { get; set; }

    /// <summary>Primary color token.</summary>
    public BrandingColorToken PrimaryColorToken { get; set; } = BrandingColorToken.Teal900;

    /// <summary>Accent color token.</summary>
    public BrandingColorToken AccentColorToken { get; set; } = BrandingColorToken.Orange;

    /// <summary>Organization support mailbox (max 256). An organizational address, not a user's.</summary>
    public string? SupportEmail { get; set; }

    /// <summary>Organization support phone line (max 20).</summary>
    public string? SupportPhone { get; set; }
}
