namespace CarePath.Application.Abstractions.Auth;

/// <summary>
/// Creates platform identities and their membership in the organization the current request
/// resolved to.
/// </summary>
public interface IIdentityProvisioningService
{
    /// <summary>
    /// Creates the platform user (or links an existing one whose id already equals the tenant
    /// user id) and an Active membership with the requested role in the current organization.
    /// </summary>
    Task<IdentityProvisioningResult> ProvisionUserAsync(
        IdentityProvisioningRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compensates a provisioning whose tenant-side transaction rolled back: the membership in
    /// the current organization is deactivated and soft-deleted so no live login outlives the
    /// tenant user. The platform identity itself is kept (it holds no organization data).
    /// </summary>
    Task CompensateAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Returns the platform user id registered for an email, or <c>null</c>.</summary>
    Task<Guid?> FindPlatformUserIdByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);
}
