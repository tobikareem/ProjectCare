using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Application.Common.Exceptions;
using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;

namespace CarePath.Application.Organization.Services;

/// <summary>Control-plane membership writes scoped to the resolved organization.</summary>
public sealed class OrganizationMembershipService : IOrganizationMembershipService
{
    private const string LastActiveAdminCode = "membership.last_active_admin";

    private readonly IOrganizationContext _organizationContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly IControlPlaneUnitOfWork _controlPlane;
    private readonly IPlatformAuditLogger _auditLogger;

    /// <summary>Initializes the service.</summary>
    public OrganizationMembershipService(
        IOrganizationContext organizationContext,
        ICurrentUserContext currentUser,
        IControlPlaneUnitOfWork controlPlane,
        IPlatformAuditLogger auditLogger)
    {
        _organizationContext = organizationContext;
        _currentUser = currentUser;
        _controlPlane = controlPlane;
        _auditLogger = auditLogger;
    }

    /// <inheritdoc />
    public async Task ChangeRoleForTenantUserAsync(Guid tenantUserId, UserRole role, CancellationToken cancellationToken = default)
    {
        var organizationId = RequireOrganization();
        var membership = await RequireMembershipAsync(organizationId, tenantUserId, cancellationToken);

        if (membership.Role == role)
        {
            return;
        }

        if (membership.Role == UserRole.Admin && role != UserRole.Admin && await IsLastActiveAdminAsync(organizationId, membership, cancellationToken))
        {
            throw new ResourceConflictException(LastActiveAdminCode, "Last active admin");
        }

        membership.Role = role;
        membership.SecurityVersion++;
        await _controlPlane.Memberships.UpdateAsync(membership, cancellationToken);
        await _controlPlane.SaveChangesAsync(cancellationToken);
        await AuditAsync(PlatformAuditAction.MembershipRoleChanged, membership, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetStatusForTenantUserAsync(Guid tenantUserId, bool active, CancellationToken cancellationToken = default)
    {
        var organizationId = RequireOrganization();
        var membership = await RequireMembershipAsync(organizationId, tenantUserId, cancellationToken);
        var target = active ? MembershipStatus.Active : MembershipStatus.Inactive;

        if (membership.Status == target)
        {
            return;
        }

        if (!active && membership.Role == UserRole.Admin && await IsLastActiveAdminAsync(organizationId, membership, cancellationToken))
        {
            throw new ResourceConflictException(LastActiveAdminCode, "Last active admin");
        }

        membership.Status = target;
        membership.SecurityVersion++;
        await _controlPlane.Memberships.UpdateAsync(membership, cancellationToken);
        await _controlPlane.SaveChangesAsync(cancellationToken);
        await AuditAsync(active ? PlatformAuditAction.MembershipReactivated : PlatformAuditAction.MembershipDeactivated, membership, cancellationToken);
    }

    private Guid RequireOrganization()
    {
        if (_organizationContext.Kind != OrganizationResolutionKind.Tenant || _organizationContext.OrganizationId is null)
        {
            throw new ResourceAccessDeniedException("OrganizationUnresolved", isPhiResource: false);
        }

        return _organizationContext.OrganizationId.Value;
    }

    private async Task<OrganizationMembership> RequireMembershipAsync(Guid organizationId, Guid platformUserId, CancellationToken cancellationToken)
    {
        var rows = await _controlPlane.Memberships.FindAsync(
            membership => membership.OrganizationId == organizationId && membership.PlatformUserId == platformUserId,
            cancellationToken);
        return rows.FirstOrDefault() ?? throw new ResourceNotFoundException(isPhiResource: false);
    }

    private async Task<bool> IsLastActiveAdminAsync(Guid organizationId, OrganizationMembership membership, CancellationToken cancellationToken)
    {
        if (!membership.IsActive)
        {
            return false;
        }

        var activeAdmins = await _controlPlane.Memberships.CountAsync(
            candidate => candidate.OrganizationId == organizationId
                && candidate.Role == UserRole.Admin
                && candidate.Status == MembershipStatus.Active,
            cancellationToken);
        return activeAdmins <= 1;
    }

    private Task AuditAsync(PlatformAuditAction action, OrganizationMembership membership, CancellationToken cancellationToken) =>
        _auditLogger.LogAsync(
            new PlatformAuditEntry(_currentUser.UserId, membership.OrganizationId, action, nameof(OrganizationMembership), membership.Id, _currentUser.CorrelationId),
            cancellationToken);
}
