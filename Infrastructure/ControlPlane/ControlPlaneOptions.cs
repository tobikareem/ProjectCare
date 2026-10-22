using CarePath.Domain.Enumerations;

namespace CarePath.Infrastructure.ControlPlane;

/// <summary>
/// Control-plane configuration bound from the <c>ControlPlane</c> section. Secrets (passwords)
/// are never part of this section's committed values; they come from user secrets or environment.
/// </summary>
public sealed class ControlPlaneOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "ControlPlane";

    /// <summary>
    /// Until CP-06 routes data planes every organization shares one tenant database, so a second
    /// Active organization would share PHI. False (default) makes startup and reactivation refuse
    /// it; true is for Development and tests with synthetic data only.
    /// </summary>
    public bool AllowMultipleActiveOrganizations { get; set; }

    /// <summary>Hosts that serve platform (operator) traffic. Configuration-only by design (D-CP04-10).</summary>
    public string[] PlatformHostNames { get; set; } = [];

    /// <summary>Base domain under which organization subdomains are generated, e.g. <c>carepathhealth.com</c>.</summary>
    public string PlatformBaseDomain { get; set; } = "localhost";

    /// <summary>The organization the existing deployment is backfilled as. Required on an empty control plane.</summary>
    public DefaultOrganizationOptions? DefaultOrganization { get; set; }

    /// <summary>First platform administrator, created on an empty control plane. Password comes from secrets only.</summary>
    public PlatformAdminOptions? PlatformAdmin { get; set; }

    /// <summary>Reverse proxies whose forwarded headers are honored.</summary>
    public TrustedProxyOptions TrustedProxies { get; set; } = new();

    /// <summary>Seconds the public branding payload may be cached. Never applies to security decisions.</summary>
    public int BrandingCacheSeconds { get; set; } = 60;

    /// <summary>Minimum seconds between audit rows for the same denied host hash on one instance (D-CP04-06).</summary>
    public int HostDenialAuditWindowSeconds { get; set; } = 60;
}

/// <summary>Default (backfilled) organization values.</summary>
public sealed class DefaultOrganizationOptions
{
    /// <summary>Registered legal name.</summary>
    public string LegalName { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Immutable slug.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>IANA time zone id.</summary>
    public string DefaultTimeZone { get; set; } = "America/New_York";

    /// <summary>Region label.</summary>
    public string DataRegion { get; set; } = "us-east";

    /// <summary>Host names registered as verified domains of the organization.</summary>
    public string[] HostNames { get; set; } = [];

    /// <summary>Locator for the existing operational database.</summary>
    public DefaultDataPlaneOptions DataPlane { get; set; } = new();
}

/// <summary>Locator values for the backfilled organization's existing database.</summary>
public sealed class DefaultDataPlaneOptions
{
    /// <summary>Database engine.</summary>
    public DataPlaneProvider Provider { get; set; } = DataPlaneProvider.SqlServer;

    /// <summary>Configuration key that holds the connection string; never the string itself.</summary>
    public string SecretReference { get; set; } = "ConnectionStrings:DefaultConnection";
}

/// <summary>Bootstrap values for the first platform administrator.</summary>
public sealed class PlatformAdminOptions
{
    /// <summary>Login email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string DisplayName { get; set; } = "Platform Operator";

    /// <summary>Initial password. Bound only from user secrets or environment; never committed.</summary>
    public string? Password { get; set; }
}

/// <summary>Reverse-proxy trust list for forwarded headers.</summary>
public sealed class TrustedProxyOptions
{
    /// <summary>Individual proxy IP addresses.</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>Proxy networks in CIDR notation.</summary>
    public string[] KnownNetworks { get; set; } = [];
}
