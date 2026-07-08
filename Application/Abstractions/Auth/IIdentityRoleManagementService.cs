namespace CarePath.Application.Abstractions.Auth;

public interface IIdentityRoleManagementService
{
    Task<bool> ReplaceUserRoleAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default);
}
