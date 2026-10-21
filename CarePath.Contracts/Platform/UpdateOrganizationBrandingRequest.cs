using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Platform;

/// <summary>Replaces an organization's branding. Colors are design-system tokens only.</summary>
public sealed class UpdateOrganizationBrandingRequest
{
    /// <summary>Private storage key of the logo blob (max 256), or <c>null</c> to clear.</summary>
    public string? LogoStorageKey { get; init; }

    /// <summary>Primary color token.</summary>
    public BrandingColorToken PrimaryColorToken { get; init; } = BrandingColorToken.Teal900;

    /// <summary>Accent color token.</summary>
    public BrandingColorToken AccentColorToken { get; init; } = BrandingColorToken.Orange;

    /// <summary>Organization support mailbox (max 256).</summary>
    public string? SupportEmail { get; init; }

    /// <summary>Organization support phone (max 20).</summary>
    public string? SupportPhone { get; init; }
}
