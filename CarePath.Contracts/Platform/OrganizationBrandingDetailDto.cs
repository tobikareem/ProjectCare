using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Platform;

/// <summary>Branding as managed by platform operators (includes the private logo storage key).</summary>
public sealed class OrganizationBrandingDetailDto
{
    /// <summary>Private storage key of the logo blob; never a public URL.</summary>
    public string? LogoStorageKey { get; init; }

    /// <summary>Primary color token.</summary>
    public BrandingColorToken PrimaryColorToken { get; init; }

    /// <summary>Accent color token.</summary>
    public BrandingColorToken AccentColorToken { get; init; }

    /// <summary>Organization support mailbox.</summary>
    public string? SupportEmail { get; init; }

    /// <summary>Organization support phone.</summary>
    public string? SupportPhone { get; init; }
}
