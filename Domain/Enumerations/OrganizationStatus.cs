namespace CarePath.Domain.Enumerations;

/// <summary>
/// Lifecycle status of a customer organization in the control plane. Only
/// <see cref="Active"/> organizations that are also <see cref="ProvisioningState.Provisioned"/>
/// admit tenant traffic.
/// </summary>
public enum OrganizationStatus
{
    /// <summary>Registered by a platform administrator; no tenant database exists yet.</summary>
    Pending = 1,

    /// <summary>Open for business. Logins and tenant traffic are admitted when provisioned.</summary>
    Active = 2,

    /// <summary>Temporarily closed by a platform administrator. All logins and tokens are refused; no data is deleted.</summary>
    Suspended = 3,

    /// <summary>Permanently closed. Records are retained for the compliance retention period.</summary>
    Offboarded = 4
}
