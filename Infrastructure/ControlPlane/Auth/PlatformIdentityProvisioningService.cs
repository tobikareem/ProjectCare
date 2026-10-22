using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.ControlPlane.Identity;
using CarePath.Infrastructure.ControlPlane.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.ControlPlane.Auth;

/// <summary>
/// Creates a platform identity for a tenant user and its Active membership in the organization
/// the request resolved to. The platform user id equals the tenant user id (D-CP04-02).
/// </summary>
public sealed class PlatformIdentityProvisioningService : IIdentityProvisioningService
{
    private readonly ControlPlaneDbContext _context;
    private readonly UserManager<PlatformUser> _userManager;
    private readonly IOrganizationContext _organizationContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly IPlatformAuditLogger _auditLogger;

    /// <summary>Initializes the service.</summary>
    public PlatformIdentityProvisioningService(
        ControlPlaneDbContext context,
        UserManager<PlatformUser> userManager,
        IOrganizationContext organizationContext,
        ICurrentUserContext currentUser,
        IPlatformAuditLogger auditLogger)
    {
        _context = context;
        _userManager = userManager;
        _organizationContext = organizationContext;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <inheritdoc />
    public async Task<IdentityProvisioningResult> ProvisionUserAsync(IdentityProvisioningRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_organizationContext.Kind != OrganizationResolutionKind.Tenant || _organizationContext.OrganizationId is null)
        {
            return IdentityProvisioningResult.Failed("OrganizationUnresolved");
        }

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: false, out var role) || !Enum.IsDefined(role))
        {
            return IdentityProvisioningResult.Failed("RoleAssignmentFailed");
        }

        var organizationId = _organizationContext.OrganizationId.Value;
        var existing = await _userManager.FindByEmailAsync(request.Email.Trim());
        PlatformUser platformUser;

        if (existing is not null)
        {
            // An existing identity can only be linked when the caller created the tenant user with
            // the same id; otherwise the platform-user/tenant-user id invariant would break.
            if (existing.Id != request.UserId)
            {
                return IdentityProvisioningResult.Failed("EmailUnavailable");
            }

            platformUser = existing;
        }
        else
        {
            platformUser = new PlatformUser
            {
                Id = request.UserId,
                UserName = request.Email,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                DisplayName = request.DisplayName,
                EmailConfirmed = true,
            };

            var created = await _userManager.CreateAsync(platformUser, request.TemporaryPassword);
            if (!created.Succeeded)
            {
                return IdentityProvisioningResult.Failed("IdentityProvisioningFailed");
            }
        }

        var hasMembership = await _context.OrganizationMemberships
            .AnyAsync(membership => membership.OrganizationId == organizationId && membership.PlatformUserId == platformUser.Id, cancellationToken);
        if (hasMembership)
        {
            return IdentityProvisioningResult.Failed("EmailUnavailable");
        }

        var newMembership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            PlatformUserId = platformUser.Id,
            TenantUserId = request.UserId,
            Role = role,
            Status = MembershipStatus.Active
        };
        _context.OrganizationMemberships.Add(newMembership);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Never leave an identity without its membership (the email would be burned for good).
            _context.Entry(newMembership).State = EntityState.Detached;
            if (existing is null)
            {
                await _userManager.DeleteAsync(platformUser);
            }

            throw;
        }

        await _auditLogger.LogAsync(
            new PlatformAuditEntry(_currentUser.UserId, organizationId, PlatformAuditAction.MembershipCreated, nameof(OrganizationMembership), newMembership.Id, _currentUser.CorrelationId),
            cancellationToken);

        return IdentityProvisioningResult.Success();
    }

    /// <inheritdoc />
    public async Task CompensateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_organizationContext.OrganizationId is null)
        {
            return;
        }

        var organizationId = _organizationContext.OrganizationId.Value;
        var membership = await _context.OrganizationMemberships
            .FirstOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.PlatformUserId == userId, cancellationToken);
        if (membership is null)
        {
            return;
        }

        membership.Status = MembershipStatus.Inactive;
        membership.IsDeleted = true;
        membership.SecurityVersion++;
        await _context.SaveChangesAsync(cancellationToken);
        await _auditLogger.LogAsync(
            new PlatformAuditEntry(_currentUser.UserId, organizationId, PlatformAuditAction.MembershipDeactivated, nameof(OrganizationMembership), membership.Id, _currentUser.CorrelationId),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Guid?> FindPlatformUserIdByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim());
        return user?.Id;
    }
}
