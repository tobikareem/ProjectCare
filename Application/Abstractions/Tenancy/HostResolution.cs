namespace CarePath.Application.Abstractions.Tenancy;

/// <summary>
/// Result of looking up a normalized host in the control plane.
/// </summary>
/// <param name="Organization">The admissible organization, or <c>null</c> when the host does not resolve.</param>
/// <param name="DenialReason">
/// Non-PHI reason code when <paramref name="Organization"/> is <c>null</c> (for logs only; never
/// surfaced to the caller so that unknown, pending, and suspended hosts stay indistinguishable).
/// </param>
public sealed record HostResolution(ResolvedOrganization? Organization, HostDenialReason DenialReason)
{
    /// <summary>True when the host resolved to an admissible organization.</summary>
    public bool IsResolved => Organization is not null;

    /// <summary>A successful resolution.</summary>
    public static HostResolution Resolved(ResolvedOrganization organization) =>
        new(organization, HostDenialReason.None);

    /// <summary>A failed resolution with a log-only reason.</summary>
    public static HostResolution Denied(HostDenialReason reason) => new(null, reason);
}

/// <summary>Log-only reason a host did not resolve. Never returned to clients.</summary>
public enum HostDenialReason
{
    /// <summary>Resolved; no denial.</summary>
    None = 0,

    /// <summary>The host could not be normalized.</summary>
    MalformedHost = 1,

    /// <summary>No domain row matches the host.</summary>
    UnknownHost = 2,

    /// <summary>The domain exists but is not verified.</summary>
    UnverifiedDomain = 3,

    /// <summary>The organization is not Active and Provisioned (pending, suspended, offboarded, or failed).</summary>
    OrganizationNotAdmissible = 4
}
