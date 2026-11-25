using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Organization;

/// <summary>
/// Approved branding fields for the organization a request host resolved to. Served anonymously
/// to the login page, so it carries organization presentation data only: no members, no users,
/// no storage keys, and no PHI.
/// </summary>
public sealed class OrganizationBrandingDto
{
    /// <summary>Organization display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Organization slug.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Primary color token.</summary>
    public BrandingColorToken PrimaryColorToken { get; init; } = BrandingColorToken.Teal900;

    /// <summary>Accent color token.</summary>
    public BrandingColorToken AccentColorToken { get; init; } = BrandingColorToken.Orange;

    /// <summary>Organization support mailbox, when published.</summary>
    public string? SupportEmail { get; init; }

    /// <summary>Organization support phone, when published.</summary>
    public string? SupportPhone { get; init; }
}
