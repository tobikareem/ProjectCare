namespace CarePath.Application.Abstractions.Auth;

public interface IIdorGuard
{
    Task<ObjectAccessResult> EnsureAuthorizedAsync(
        ProtectedResourceType resourceType,
        Guid resourceId,
        ObjectAccessAction action,
        CancellationToken cancellationToken = default);
}