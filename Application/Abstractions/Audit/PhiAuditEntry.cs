using CarePath.Application.Abstractions.Auth;

namespace CarePath.Application.Abstractions.Audit;

public sealed record PhiAuditEntry(
    Guid? ActorUserId,
    AuditActorType ActorType,
    DateTime TimestampUtc,
    AuditAction Action,
    ProtectedResourceType EntityType,
    Guid EntityId,
    string? CorrelationId,
    string? BackgroundJobName = null,
    IReadOnlyDictionary<string, string>? Attributes = null);
