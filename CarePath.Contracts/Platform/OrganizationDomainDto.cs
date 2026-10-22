using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Platform;

/// <summary>A host name registered to an organization.</summary>
public sealed class OrganizationDomainDto
{
    /// <summary>Domain row identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Normalized host name.</summary>
    public string HostName { get; init; } = string.Empty;

    /// <summary>True for the canonical host.</summary>
    public bool IsPrimary { get; init; }

    /// <summary>Whether the host resolves.</summary>
    public DomainVerificationStatus VerificationStatus { get; init; }
}
