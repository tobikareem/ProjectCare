using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;

namespace CarePath.Application.Auth;

public sealed class IdorGuard : IIdorGuard
{
    private const string UndisclosedDeniedCode = "ResourceUnavailable";

    private readonly ICurrentUserContext currentUser;
    private readonly IObjectAuthorizationService objectAuthorizationService;
    private readonly IPhiAuditLogger auditLogger;

    public IdorGuard(
        ICurrentUserContext currentUser,
        IObjectAuthorizationService objectAuthorizationService,
        IPhiAuditLogger auditLogger)
    {
        this.currentUser = currentUser;
        this.objectAuthorizationService = objectAuthorizationService;
        this.auditLogger = auditLogger;
    }

    public async Task<ObjectAccessResult> EnsureAuthorizedAsync(
        ProtectedResourceType resourceType,
        Guid resourceId,
        ObjectAccessAction action,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            await LogAccessAsync(resourceType, resourceId, AuditAction.AccessDenied, cancellationToken);
            return ObjectAccessResult.DeniedWithoutDisclosure(UndisclosedDeniedCode);
        }

        var request = new ObjectAccessRequest(
            currentUser,
            resourceType,
            resourceId,
            action,
            currentUser.CorrelationId);

        var authorizationResult = await objectAuthorizationService.AuthorizeAsync(request, cancellationToken);
        if (authorizationResult.IsAuthorized)
        {
            await LogAccessAsync(resourceType, resourceId, ToAuditAction(action), cancellationToken);
            return ObjectAccessResult.Authorized();
        }

        await LogAccessAsync(resourceType, resourceId, AuditAction.AccessDenied, cancellationToken);
        return ObjectAccessResult.DeniedWithoutDisclosure(UndisclosedDeniedCode);
    }

    private Task LogAccessAsync(
        ProtectedResourceType resourceType,
        Guid resourceId,
        AuditAction action,
        CancellationToken cancellationToken)
    {
        var entry = new PhiAuditEntry(
            currentUser.UserId,
            GetActorType(),
            DateTime.UtcNow,
            action,
            resourceType,
            resourceId,
            currentUser.CorrelationId);

        return auditLogger.LogAsync(entry, cancellationToken);
    }

    private AuditActorType GetActorType() => currentUser.UserId is null
        ? AuditActorType.Anonymous
        : AuditActorType.User;

    private static AuditAction ToAuditAction(ObjectAccessAction action) => action switch
    {
        ObjectAccessAction.Read => AuditAction.Read,
        ObjectAccessAction.Create => AuditAction.Create,
        ObjectAccessAction.Update => AuditAction.Update,
        ObjectAccessAction.Delete => AuditAction.Delete,
        _ => AuditAction.AccessDenied
    };
}