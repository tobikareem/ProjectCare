namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.ProvisioningState</c>. Data-plane provisioning state of an organization.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum ProvisioningState
{
    /// <summary>Registered.</summary>
    Registered = 1,

    /// <summary>Provisioning.</summary>
    Provisioning = 2,

    /// <summary>Provisioned.</summary>
    Provisioned = 3,

    /// <summary>Failed.</summary>
    Failed = 4
}
