namespace CarePath.Contracts.Platform;

/// <summary>Data-plane locator summary. Carries the provider and schema projection only, never a connection string or secret key.</summary>
public sealed class OrganizationDataPlaneDto
{
    /// <summary>Database engine name.</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>Admission projection of the applied schema version, when known.</summary>
    public string? SchemaVersion { get; init; }

    /// <summary>UTC registration timestamp.</summary>
    public DateTime RegisteredAtUtc { get; init; }
}
