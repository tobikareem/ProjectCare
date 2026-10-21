using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Platform;

/// <summary>Organization row for platform-operator lists. Non-PHI.</summary>
public sealed class OrganizationSummaryDto
{
    /// <summary>Organization identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Immutable slug.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Lifecycle status.</summary>
    public OrganizationStatus Status { get; init; }

    /// <summary>Data-plane provisioning state.</summary>
    public ProvisioningState ProvisioningState { get; init; }

    /// <summary>Primary host name, when one is registered.</summary>
    public string? PrimaryHostName { get; init; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; init; }
}
