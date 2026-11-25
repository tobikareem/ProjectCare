using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Platform;

/// <summary>
/// Append-only, non-PHI platform audit record. No update or delete path exists in code: the
/// record is written only through the platform audit logger and is never exposed through a
/// generic repository.
/// </summary>
/// <remarks>
/// Never stores an email address, a host name, or any PHI. Denied host resolutions record
/// <see cref="HostHash"/> (SHA-256 of the normalized host) only.
/// </remarks>
public class PlatformAuditEvent : BaseEntity
{
    /// <summary>Platform user who performed the action; <c>null</c> for anonymous or system actions.</summary>
    public Guid? ActorPlatformUserId { get; set; }

    /// <summary>Organization the action concerned, when known.</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>What happened.</summary>
    public PlatformAuditAction Action { get; set; }

    /// <summary>Control-plane entity type name (max 64).</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Identifier of the affected entity, when one exists.</summary>
    public Guid? EntityId { get; set; }

    /// <summary>Request correlation identifier (max 64).</summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Lower-case hex SHA-256 of the normalized host for denied resolutions (64 chars). Never the
    /// host string; pseudonymous rather than anonymous, since a known host can be re-hashed to match.
    /// </summary>
    public string? HostHash { get; set; }

    /// <summary>UTC timestamp of the action.</summary>
    public DateTime OccurredAtUtc { get; set; }
}
