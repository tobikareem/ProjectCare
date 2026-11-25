namespace CarePath.Contracts.Platform;

/// <summary>Updates an organization's names, time zone, and region. The slug is immutable.</summary>
public sealed class UpdateOrganizationRequest
{
    /// <summary>Registered legal name (max 200).</summary>
    public string LegalName { get; init; } = string.Empty;

    /// <summary>Display name (max 100).</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>IANA time zone id.</summary>
    public string DefaultTimeZone { get; init; } = string.Empty;

    /// <summary>Deployment region label (max 32).</summary>
    public string DataRegion { get; init; } = string.Empty;
}
