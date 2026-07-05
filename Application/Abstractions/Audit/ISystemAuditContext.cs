using CarePath.Application.Abstractions.Auth;

namespace CarePath.Application.Abstractions.Audit;

public interface ISystemAuditContext
{
    PhiAuditEntry CreateBackgroundJobEntry(
        AuditAction action,
        ProtectedResourceType entityType,
        Guid entityId,
        string backgroundJobName,
        string? correlationId);
}