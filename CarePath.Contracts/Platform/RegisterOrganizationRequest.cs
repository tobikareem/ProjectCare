namespace CarePath.Contracts.Platform;

/// <summary>Registers a new organization in Pending / Registered state (no database is created).</summary>
public sealed class RegisterOrganizationRequest
{
    /// <summary>Registered legal name (max 200).</summary>
    public string LegalName { get; init; } = string.Empty;

    /// <summary>Display name (max 100).</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Immutable slug: 3–40 lower-case letters, digits, single hyphens; not reserved.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>IANA time zone id.</summary>
    public string DefaultTimeZone { get; init; } = "America/New_York";

    /// <summary>Deployment region label (max 32).</summary>
    public string DataRegion { get; init; } = "us-east";
}
