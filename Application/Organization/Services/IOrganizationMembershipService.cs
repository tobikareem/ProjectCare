using CarePath.Domain.Enumerations;

namespace CarePath.Application.Organization.Services;

/// <summary>
/// Membership lifecycle for the organization the current request resolved to. The membership is
/// the sole source of truth for a user's role (ADR 0003 §10); <c>SecurityVersion</c> increments
/// on every change (D-CP04-05).
/// </summary>
public interface IOrganizationMembershipService
{
    /// <summary>Changes the role on the current organization's membership for a tenant user.</summary>
    /// <param name="tenantUserId">Tenant <c>User.Id</c> (equals the platform user id).</param>
    /// <param name="role">New tenant role.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <exception cref="Common.Exceptions.ResourceNotFoundException">No membership exists in the current organization.</exception>
    /// <exception cref="Common.Exceptions.ResourceConflictException">The membership is the last active Admin.</exception>
    Task ChangeRoleForTenantUserAsync(Guid tenantUserId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>Activates or deactivates the current organization's membership for a tenant user.</summary>
    /// <param name="tenantUserId">Tenant <c>User.Id</c>.</param>
    /// <param name="active">True to activate, false to deactivate.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    Task SetStatusForTenantUserAsync(Guid tenantUserId, bool active, CancellationToken cancellationToken = default);
}
