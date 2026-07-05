using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Clients;

/// <summary>
/// A family-proxy access grant (D-S4-1). PHI-adjacent access-control record: endpoints
/// returning this DTO are Admin/Coordinator only and every read/write is audit logged.
/// </summary>
public class ClientAccessGrantDto
{
    /// <summary>Grant identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Client the grant applies to.</summary>
    public Guid ClientId { get; init; }

    /// <summary>User (Client role) receiving access.</summary>
    public Guid GranteeUserId { get; init; }

    /// <summary>Grantee display name.</summary>
    public string GranteeFullName { get; init; } = string.Empty;

    /// <summary>Scope of access (patient-facing only, or full Client-role access).</summary>
    public AccessScope Scope { get; init; }

    /// <summary>User who granted access.</summary>
    public Guid GrantedByUserId { get; init; }

    /// <summary>When access was granted (UTC).</summary>
    public DateTime GrantedAtUtc { get; init; }

    /// <summary>When access was revoked (UTC), if revoked. Revoked grants never authorize.</summary>
    public DateTime? RevokedAtUtc { get; init; }
}
