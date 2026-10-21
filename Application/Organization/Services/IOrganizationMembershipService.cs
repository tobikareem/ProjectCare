using CarePath.Contracts.Common;
using CarePath.Contracts.Organization;
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

    /// <summary>Lists the current organization's memberships.</summary>
    Task<PagedResult<MembershipDto>> ListAsync(PagedRequest paging, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a membership in the current organization: links an existing platform identity or
    /// creates a new one, then creates the tenant profile with the same id when none exists.
    /// Control plane first, tenant second, with compensating soft delete of the membership; no
    /// distributed transaction. The response never reveals whether the identity pre-existed.
    /// </summary>
    Task<MembershipDto> CreateAsync(CreateMembershipRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes a membership's role by membership id (scoped to the current organization) and mirrors the tenant profile.</summary>
    Task<MembershipDto> ChangeRoleAsync(Guid membershipId, UpdateMembershipRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Activates or deactivates a membership by id (scoped to the current organization) and mirrors the tenant profile.</summary>
    Task<MembershipDto> SetStatusAsync(Guid membershipId, UpdateMembershipStatusRequest request, CancellationToken cancellationToken = default);
}
