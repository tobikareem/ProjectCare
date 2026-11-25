namespace CarePath.Contracts.Platform;

/// <summary>Adds a host name to an organization. System subdomains verify on creation; other hosts stay Pending.</summary>
public sealed class AddOrganizationDomainRequest
{
    /// <summary>Host name (RFC 1123, max 253).</summary>
    public string HostName { get; init; } = string.Empty;

    /// <summary>Make this the canonical host.</summary>
    public bool IsPrimary { get; init; }
}
