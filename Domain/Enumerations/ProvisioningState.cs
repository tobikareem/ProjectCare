namespace CarePath.Domain.Enumerations;

/// <summary>
/// Data-plane provisioning state of an organization. CP-04 only ever writes
/// <see cref="Registered"/> (new organizations) and <see cref="Provisioned"/> (the backfilled
/// deployment); CP-06 owns the transitions between them.
/// </summary>
public enum ProvisioningState
{
    /// <summary>Control-plane record exists; no operational database has been created.</summary>
    Registered = 1,

    /// <summary>A provisioning run is in progress.</summary>
    Provisioning = 2,

    /// <summary>The operational database exists and is registered in the data-plane locator.</summary>
    Provisioned = 3,

    /// <summary>The last provisioning run failed; operator intervention is required.</summary>
    Failed = 4
}
