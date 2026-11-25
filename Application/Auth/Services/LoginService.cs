using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Contracts.Auth;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using ContractAuthTokenKind = CarePath.Contracts.Enumerations.AuthTokenKind;
using ContractUserRole = CarePath.Contracts.Enumerations.UserRole;

namespace CarePath.Application.Auth.Services;

/// <summary>
/// Issues sessions bound to exactly one organization (or to the platform). Credentials are
/// checked by the identity store; the membership for the resolved organization is the sole
/// source of the role (ADR 0003 §10). Every failure path returns the same result.
/// </summary>
public sealed class LoginService : ILoginService
{
    private readonly IOrganizationContext _organizationContext;
    private readonly IIdentityService _identityService;
    private readonly IControlPlaneUnitOfWork _controlPlane;
    private readonly IUnitOfWork _tenant;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPlatformAuditLogger _auditLogger;

    /// <summary>Initializes the login service.</summary>
    public LoginService(
        IOrganizationContext organizationContext,
        IIdentityService identityService,
        IControlPlaneUnitOfWork controlPlane,
        IUnitOfWork tenant,
        IJwtTokenService jwtTokenService,
        IPlatformAuditLogger auditLogger)
    {
        _organizationContext = organizationContext;
        _identityService = identityService;
        _controlPlane = controlPlane;
        _tenant = tenant;
        _jwtTokenService = jwtTokenService;
        _auditLogger = auditLogger;
    }

    /// <inheritdoc />
    public async Task<LoginResult> LoginAsync(string email, string password, string? correlationId, CancellationToken cancellationToken = default)
    {
        if (_organizationContext.Kind == OrganizationResolutionKind.Unresolved)
        {
            return LoginResult.Failed;
        }

        var user = await _identityService.ValidateCredentialsAsync(email, password, cancellationToken);
        if (!user.Succeeded || user.UserId is null)
        {
            return LoginResult.Failed;
        }

        if (_organizationContext.Kind == OrganizationResolutionKind.Platform)
        {
            if (!user.IsPlatformAdmin)
            {
                await AuditAsync(PlatformAuditAction.LoginDeniedNotPlatformAdmin, user.UserId.Value, null, correlationId, cancellationToken);
                return LoginResult.Failed;
            }

            var platformRefresh = await _identityService.IssueRefreshTokenAsync(user.UserId.Value, null, null, cancellationToken);
            return await IssuePlatformSessionAsync(user, platformRefresh, correlationId, cancellationToken);
        }

        var organizationId = _organizationContext.OrganizationId!.Value;
        var membership = await FindMembershipAsync(organizationId, user.UserId.Value, cancellationToken);
        if (membership is null)
        {
            await AuditAsync(PlatformAuditAction.LoginDeniedNoMembership, user.UserId.Value, organizationId, correlationId, cancellationToken);
            return LoginResult.Failed;
        }

        var (admissible, tenantUser) = await IsAdmissibleAsync(membership, cancellationToken);
        if (!admissible)
        {
            await AuditAsync(PlatformAuditAction.LoginDeniedInactiveMembership, user.UserId.Value, organizationId, correlationId, cancellationToken);
            return LoginResult.Failed;
        }

        var refreshToken = await _identityService.IssueRefreshTokenAsync(user.UserId.Value, membership.Id, organizationId, cancellationToken);
        await RecordLoginAsync(membership, tenantUser, cancellationToken);
        return await IssueTenantSessionAsync(user, membership, refreshToken, correlationId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LoginResult> RefreshAsync(string refreshToken, string? correlationId, CancellationToken cancellationToken = default)
    {
        if (_organizationContext.Kind == OrganizationResolutionKind.Unresolved)
        {
            return LoginResult.Failed;
        }

        var binding = _organizationContext.Kind == OrganizationResolutionKind.Platform
            ? RefreshTokenBinding.Platform
            : RefreshTokenBinding.ForOrganization(_organizationContext.OrganizationId!.Value);

        var rotation = await _identityService.RotateRefreshTokenAsync(refreshToken, binding, cancellationToken);
        if (!rotation.Succeeded || rotation.User?.UserId is null || rotation.RefreshToken is null)
        {
            return LoginResult.Failed;
        }

        if (binding.IsPlatform)
        {
            return rotation.User.IsPlatformAdmin
                ? await IssuePlatformSessionAsync(rotation.User, rotation.RefreshToken, correlationId, cancellationToken)
                : LoginResult.Failed;
        }

        if (rotation.MembershipId is null || rotation.OrganizationId != binding.OrganizationId)
        {
            return LoginResult.Failed;
        }

        var membership = await _controlPlane.Memberships.GetByIdAsync(rotation.MembershipId.Value, cancellationToken);
        if (membership is null || membership.OrganizationId != binding.OrganizationId)
        {
            return LoginResult.Failed;
        }

        // Same admissibility rule as login: an inactive membership or tenant profile cannot refresh.
        var (admissible, _) = await IsAdmissibleAsync(membership, cancellationToken);
        if (!admissible)
        {
            await AuditAsync(PlatformAuditAction.LoginDeniedInactiveMembership, rotation.User.UserId.Value, membership.OrganizationId, correlationId, cancellationToken);
            return LoginResult.Failed;
        }

        return await IssueTenantSessionAsync(rotation.User, membership, rotation.RefreshToken, correlationId, cancellationToken);
    }

    private async Task<OrganizationMembership?> FindMembershipAsync(Guid organizationId, Guid platformUserId, CancellationToken cancellationToken)
    {
        var rows = await _controlPlane.Memberships.FindAsync(
            membership => membership.OrganizationId == organizationId && membership.PlatformUserId == platformUserId,
            cancellationToken);
        return rows.FirstOrDefault();
    }

    // A membership whose tenant profile no longer exists (a rolled-back provisioning, a removed
    // user) or is inactive must not log in or refresh: the data plane is the authority on whether
    // the person is active there.
    private async Task<(bool Admissible, User? TenantUser)> IsAdmissibleAsync(OrganizationMembership membership, CancellationToken cancellationToken)
    {
        if (!membership.IsActive)
        {
            return (false, null);
        }

        if (membership.TenantUserId is null)
        {
            return (true, null);
        }

        var tenantUser = await _tenant.Users.GetByIdAsync(membership.TenantUserId.Value, cancellationToken);
        return tenantUser is { IsActive: true } ? (true, tenantUser) : (false, null);
    }

    private async Task RecordLoginAsync(OrganizationMembership membership, User? tenantUser, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        membership.LastLoginAtUtc = now;
        await _controlPlane.Memberships.UpdateAsync(membership, cancellationToken);
        await _controlPlane.SaveChangesAsync(cancellationToken);

        if (tenantUser is null)
        {
            return;
        }

        tenantUser.LastLoginAt = now;
        await _tenant.Users.UpdateAsync(tenantUser, cancellationToken);
        await _tenant.SaveChangesAsync(cancellationToken);
    }

    private async Task<LoginResult> IssueTenantSessionAsync(
        IdentityUserResult user,
        OrganizationMembership membership,
        string refreshToken,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var roleName = membership.Role.ToString();
        var jwt = await _jwtTokenService.CreateTokenAsync(
            new JwtTokenRequest(
                user.UserId!.Value,
                user.Email ?? string.Empty,
                new HashSet<string>([roleName], StringComparer.Ordinal),
                correlationId,
                CarePathClaimTypes.TenantTokenKind,
                membership.OrganizationId,
                membership.Id),
            cancellationToken);

        return new LoginResult(true, new AuthTokenResponse
        {
            AccessToken = jwt.AccessToken,
            ExpiresAtUtc = jwt.ExpiresAtUtc,
            RefreshToken = refreshToken,
            TokenKind = ContractAuthTokenKind.Tenant,
            Role = (ContractUserRole)(int)membership.Role,
            DisplayName = user.DisplayName ?? string.Empty,
            OrganizationId = membership.OrganizationId,
            OrganizationSlug = _organizationContext.Slug,
            OrganizationDisplayName = _organizationContext.DisplayName,
            MembershipId = membership.Id,
        });
    }

    private async Task<LoginResult> IssuePlatformSessionAsync(
        IdentityUserResult user,
        string refreshToken,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var jwt = await _jwtTokenService.CreateTokenAsync(
            new JwtTokenRequest(
                user.UserId!.Value,
                user.Email ?? string.Empty,
                new HashSet<string>([ApplicationRoles.PlatformAdmin], StringComparer.Ordinal),
                correlationId,
                CarePathClaimTypes.PlatformTokenKind,
                null,
                null),
            cancellationToken);

        return new LoginResult(true, new AuthTokenResponse
        {
            AccessToken = jwt.AccessToken,
            ExpiresAtUtc = jwt.ExpiresAtUtc,
            RefreshToken = refreshToken,
            TokenKind = ContractAuthTokenKind.Platform,
            Role = null,
            DisplayName = user.DisplayName ?? string.Empty,
        });
    }

    private Task AuditAsync(PlatformAuditAction action, Guid actorId, Guid? organizationId, string? correlationId, CancellationToken cancellationToken) =>
        _auditLogger.LogAsync(
            new PlatformAuditEntry(actorId, organizationId, action, nameof(OrganizationMembership), null, correlationId),
            cancellationToken);
}
