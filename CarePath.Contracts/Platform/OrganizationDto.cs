using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Platform;

/// <summary>Organization detail for platform operators. Non-PHI; never includes member or user data.</summary>
public sealed class OrganizationDto
{
    /// <summary>Organization identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Registered legal name.</summary>
    public string LegalName { get; init; } = string.Empty;

    /// <summary>Display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Immutable slug.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Lifecycle status.</summary>
    public OrganizationStatus Status { get; init; }

    /// <summary>Data-plane provisioning state.</summary>
    public ProvisioningState ProvisioningState { get; init; }

    /// <summary>IANA time zone id.</summary>
    public string DefaultTimeZone { get; init; } = string.Empty;

    /// <summary>Deployment region label.</summary>
    public string DataRegion { get; init; } = string.Empty;

    /// <summary>Operator-entered suspension reason, when suspended.</summary>
    public string? SuspensionReason { get; init; }

    /// <summary>UTC timestamp of the latest suspension.</summary>
    public DateTime? SuspendedAtUtc { get; init; }

    /// <summary>Registered host names.</summary>
    public IReadOnlyList<OrganizationDomainDto> Domains { get; init; } = [];

    /// <summary>Data-plane locator summary; <c>null</c> until provisioned.</summary>
    public OrganizationDataPlaneDto? DataPlane { get; init; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; init; }
}
