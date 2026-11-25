using CarePath.Domain.Enumerations;

namespace CarePath.Application.Abstractions.Audit;

/// <summary>
/// Non-PHI platform audit entry. Never carries an email address, a host name, or PHI.
/// </summary>
/// <param name="ActorPlatformUserId">Acting platform user, or <c>null</c> for anonymous/system actions.</param>
/// <param name="OrganizationId">Organization concerned, when known.</param>
/// <param name="Action">What happened.</param>
/// <param name="EntityType">Control-plane entity type name.</param>
/// <param name="EntityId">Affected entity id, when one exists.</param>
/// <param name="CorrelationId">Request correlation id.</param>
/// <param name="HostHash">SHA-256 hex of the normalized host for denied resolutions; otherwise <c>null</c>.</param>
public sealed record PlatformAuditEntry(
    Guid? ActorPlatformUserId,
    Guid? OrganizationId,
    PlatformAuditAction Action,
    string EntityType,
    Guid? EntityId,
    string? CorrelationId,
    string? HostHash = null);
