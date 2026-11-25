using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Common.Paging;
using CarePath.Application.Organization.Mapping;
using CarePath.Application.Organization.Validators;
using CarePath.Contracts.Common;
using CarePath.Contracts.Organization;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentValidation;

namespace CarePath.Application.Organization.Services;

/// <summary>Control-plane membership writes scoped to the resolved organization.</summary>
public sealed class OrganizationMembershipService : IOrganizationMembershipService
{
    private const string LastActiveAdminCode = "membership.last_active_admin";

    private readonly IOrganizationContext _organizationContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly IControlPlaneUnitOfWork _controlPlane;
    private readonly IPlatformAuditLogger _auditLogger;
    private readonly IUnitOfWork _tenant;
    private readonly IPlatformUserDirectory _directory;
    private readonly IIdentityProvisioningService _provisioning;
    private readonly IValidator<CreateMembershipRequest> _createValidator;
    private readonly IValidator<UpdateMembershipRoleRequest> _roleValidator;

    /// <summary>Initializes the service.</summary>
    public OrganizationMembershipService(
        IOrganizationContext organizationContext,
        ICurrentUserContext currentUser,
        IControlPlaneUnitOfWork controlPlane,
        IPlatformAuditLogger auditLogger,
        IUnitOfWork tenant,
        IPlatformUserDirectory directory,
        IIdentityProvisioningService provisioning,
        IValidator<CreateMembershipRequest>? createValidator = null,
        IValidator<UpdateMembershipRoleRequest>? roleValidator = null)
    {
        _organizationContext = organizationContext;
        _currentUser = currentUser;
        _controlPlane = controlPlane;
        _auditLogger = auditLogger;
        _tenant = tenant;
        _directory = directory;
        _provisioning = provisioning;
        _createValidator = createValidator ?? new CreateMembershipRequestValidator();
        _roleValidator = roleValidator ?? new UpdateMembershipRoleRequestValidator();
    }

    /// <inheritdoc />
    public async Task<PagedResult<MembershipDto>> ListAsync(PagedRequest paging, CancellationToken cancellationToken = default)
    {
        var organizationId = RequireOrganization();

        // Repository paging orders by Id, a stable key; names live in the directory, not on the row.
        var (memberships, total) = await _controlPlane.Memberships.GetPagedAsync(
            membership => membership.OrganizationId == organizationId,
            paging.PageNumber,
            paging.PageSize,
            cancellationToken);

        var users = await _directory.GetSummariesAsync(memberships.Select(membership => membership.PlatformUserId).ToArray(), cancellationToken);
        var items = memberships
            .Select(membership => MembershipContractMapper.ToDto(membership, users.GetValueOrDefault(membership.PlatformUserId)))
            .ToArray();
        return PagedResultFactory.Create(items, total, paging.PageNumber, paging.PageSize);
    }

    /// <inheritdoc />
    public async Task<MembershipDto> CreateAsync(CreateMembershipRequest request, CancellationToken cancellationToken = default)
    {
        var organizationId = RequireOrganization();
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var email = request.Email.Trim();
        var role = (UserRole)(int)request.Role;

        // ENUMERATION SAFETY: whether the email already belongs to a platform identity (in another
        // organization) is never revealed. Both paths take the same inputs, return the same DTO
        // shape built from the request, and fail with the same generic codes.
        var existingIdentity = await _directory.FindByEmailAsync(email, cancellationToken);
        var userId = existingIdentity?.Id ?? Guid.NewGuid();

        if (await _controlPlane.Memberships.ExistsAsync(
                membership => membership.OrganizationId == organizationId && membership.PlatformUserId == userId, cancellationToken))
        {
            throw new ResourceConflictException("membership.exists", "A membership already exists.");
        }

        // The platform user id doubles as the tenant user id (D-CP04-02).
        var tenantUser = await _tenant.Users.GetByIdAsync(userId, cancellationToken);
        if (tenantUser is null && await _tenant.Users.ExistsAsync(user => user.Email == email, cancellationToken))
        {
            throw new ValidationException("The request is invalid.");
        }

        var displayName = $"{request.FirstName.Trim()} {request.LastName.Trim()}";

        // Phase 1 (control plane): identity when new, then the membership (design §5 two-phase write).
        var membership = existingIdentity is null
            ? await ProvisionIdentityAndMembershipAsync(organizationId, userId, email, request, displayName, role, cancellationToken)
            : await AddMembershipAsync(organizationId, userId, role, cancellationToken);

        // Phase 2 (tenant): the profile row with the same id. On failure the membership is
        // compensated (soft-deleted) so no live membership points at a missing profile; the
        // filtered unique indexes let a retry succeed. No distributed transaction, by design.
        if (tenantUser is null)
        {
            try
            {
                tenantUser = new User
                {
                    Id = userId,
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = email,
                    PhoneNumber = request.PhoneNumber.Trim(),
                    Role = role,
                    IsActive = true,
                    PlatformUserId = userId,
                };
                await _tenant.Users.AddAsync(tenantUser, cancellationToken);
                await _tenant.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await CompensateMembershipAsync(membership, existingIdentity is null, cancellationToken);
                throw;
            }
        }

        if (existingIdentity is not null)
        {
            await AuditAsync(PlatformAuditAction.MembershipCreated, membership, cancellationToken);
        }

        return MembershipContractMapper.ToDto(membership, new PlatformUserSummary(userId, email, displayName));
    }

    /// <inheritdoc />
    public async Task<MembershipDto> ChangeRoleAsync(Guid membershipId, UpdateMembershipRoleRequest request, CancellationToken cancellationToken = default)
    {
        var organizationId = RequireOrganization();
        await _roleValidator.ValidateAndThrowAsync(request, cancellationToken);
        var membership = await RequireMembershipByIdAsync(organizationId, membershipId, cancellationToken);
        var role = (UserRole)(int)request.Role;
        var previousRole = membership.Role;

        await ApplyRoleAsync(organizationId, membership, role, cancellationToken);
        await MirrorOrRevertAsync(
            membership,
            user => user.Role = role,
            revert => revert.Role = previousRole,
            cancellationToken);
        return await ToDtoAsync(membership, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MembershipDto> SetStatusAsync(Guid membershipId, UpdateMembershipStatusRequest request, CancellationToken cancellationToken = default)
    {
        var organizationId = RequireOrganization();
        var membership = await RequireMembershipByIdAsync(organizationId, membershipId, cancellationToken);
        var active = request.IsActive;
        var previousStatus = membership.Status;

        await ApplyStatusAsync(organizationId, membership, active, cancellationToken);
        await MirrorOrRevertAsync(
            membership,
            user => user.IsActive = active,
            revert => revert.Status = previousStatus,
            cancellationToken);
        return await ToDtoAsync(membership, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ChangeRoleForTenantUserAsync(Guid tenantUserId, UserRole role, CancellationToken cancellationToken = default)
    {
        var organizationId = RequireOrganization();
        var membership = await RequireMembershipAsync(organizationId, tenantUserId, cancellationToken);
        await ApplyRoleAsync(organizationId, membership, role, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetStatusForTenantUserAsync(Guid tenantUserId, bool active, CancellationToken cancellationToken = default)
    {
        var organizationId = RequireOrganization();
        var membership = await RequireMembershipAsync(organizationId, tenantUserId, cancellationToken);
        await ApplyStatusAsync(organizationId, membership, active, cancellationToken);
    }

    private async Task ApplyRoleAsync(Guid organizationId, OrganizationMembership membership, UserRole role, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ValidationException("The request is invalid.");
        }

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

    private async Task ApplyStatusAsync(Guid organizationId, OrganizationMembership membership, bool active, CancellationToken cancellationToken)
    {
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

    private async Task<OrganizationMembership> ProvisionIdentityAndMembershipAsync(
        Guid organizationId,
        Guid userId,
        string email,
        CreateMembershipRequest request,
        string displayName,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var provisioned = await _provisioning.ProvisionUserAsync(
            new IdentityProvisioningRequest(userId, email, request.PhoneNumber.Trim(), displayName, request.TemporaryPassword, role.ToString()),
            cancellationToken);
        if (!provisioned.Succeeded)
        {
            throw new ValidationException("The account could not be provisioned.");
        }

        // The provisioning service creates and audits the membership (TenantUserId = userId).
        return (await _controlPlane.Memberships.FindAsync(
            candidate => candidate.OrganizationId == organizationId && candidate.PlatformUserId == userId, cancellationToken)).First();
    }

    private async Task<OrganizationMembership> AddMembershipAsync(Guid organizationId, Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        var membership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            PlatformUserId = userId,
            TenantUserId = userId,
            Role = role,
            Status = MembershipStatus.Active,
        };
        await _controlPlane.Memberships.AddAsync(membership, cancellationToken);
        await _controlPlane.SaveChangesAsync(cancellationToken);
        return membership;
    }

    // Best effort: a secondary failure must not mask the original exception, and no detail of
    // either is logged here (the caller's exception carries no PHI by construction).
    private async Task CompensateMembershipAsync(OrganizationMembership membership, bool provisionedIdentity, CancellationToken cancellationToken)
    {
        try
        {
            if (provisionedIdentity)
            {
                await _provisioning.CompensateAsync(membership.PlatformUserId, cancellationToken);
                return;
            }

            await _controlPlane.Memberships.DeleteAsync(membership, cancellationToken);
            await _controlPlane.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Swallowed deliberately: the original failure propagates; the orphaned membership is
            // inactive-by-absence of a tenant profile and is reconciled by EnsureMembershipsAsync.
        }
    }

    // User.Role / User.IsActive stay as read-only mirrors of the membership until CP-05
    // (D-CP04-09). The mirror runs even when the membership was unchanged so drift heals; when
    // the tenant write fails the membership change is reverted so the two never disagree.
    private async Task MirrorOrRevertAsync(
        OrganizationMembership membership,
        Action<User> apply,
        Action<OrganizationMembership> revert,
        CancellationToken cancellationToken)
    {
        if (membership.TenantUserId is null)
        {
            return;
        }

        var user = await _tenant.Users.GetByIdAsync(membership.TenantUserId.Value, cancellationToken);
        if (user is null)
        {
            return;
        }

        try
        {
            apply(user);
            await _tenant.Users.UpdateAsync(user, cancellationToken);
            await _tenant.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            revert(membership);
            membership.SecurityVersion++;
            await _controlPlane.Memberships.UpdateAsync(membership, cancellationToken);
            await _controlPlane.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task<OrganizationMembership> RequireMembershipByIdAsync(Guid organizationId, Guid membershipId, CancellationToken cancellationToken)
    {
        var membership = await _controlPlane.Memberships.GetByIdAsync(membershipId, cancellationToken);
        // A membership of another organization is not found, not forbidden: no cross-tenant oracle.
        return membership is not null && membership.OrganizationId == organizationId
            ? membership
            : throw new ResourceNotFoundException(isPhiResource: false);
    }

    private async Task<MembershipDto> ToDtoAsync(OrganizationMembership membership, CancellationToken cancellationToken)
    {
        var users = await _directory.GetSummariesAsync([membership.PlatformUserId], cancellationToken);
        return MembershipContractMapper.ToDto(membership, users.GetValueOrDefault(membership.PlatformUserId));
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
