namespace CarePath.Application.Abstractions.Auth;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(
        ICurrentUserContext user,
        string permission,
        CancellationToken cancellationToken = default);
}