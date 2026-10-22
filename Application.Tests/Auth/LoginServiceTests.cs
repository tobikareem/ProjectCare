using System.Linq.Expressions;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Application.Auth.Services;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentAssertions;
using Moq;
using ContractAuthTokenKind = CarePath.Contracts.Enumerations.AuthTokenKind;
using ContractUserRole = CarePath.Contracts.Enumerations.UserRole;

namespace CarePath.Application.Tests.Auth;

public sealed class LoginServiceTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Login_WhenMembershipIsActive_IssuesTenantSessionWithMembershipRole()
    {
        // Arrange
        var membership = Membership(UserRole.Coordinator, MembershipStatus.Active);
        var harness = new Harness(OrganizationResolutionKind.Tenant, membership);

        // Act
        var result = await harness.Service.LoginAsync("person@carepath.local", "ValidPassword1", "corr-1");

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Token!.TokenKind.Should().Be(ContractAuthTokenKind.Tenant);
        result.Token.Role.Should().Be(ContractUserRole.Coordinator);
        result.Token.OrganizationId.Should().Be(OrganizationId);
        result.Token.MembershipId.Should().Be(membership.Id);
        result.Token.OrganizationSlug.Should().Be("brightcare");
        harness.LastJwtRequest!.TokenKind.Should().Be(CarePathClaimTypes.TenantTokenKind);
        harness.LastJwtRequest.Roles.Should().Equal("Coordinator");
        harness.IssuedRefresh.Should().Be((UserId, (Guid?)membership.Id, (Guid?)OrganizationId));
        membership.LastLoginAtUtc.Should().NotBeNull();
        harness.TenantUser.LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_WhenUserHasNoMembershipAtThisOrganization_FailsAndAuditsWithoutEmail()
    {
        // Arrange
        var harness = new Harness(OrganizationResolutionKind.Tenant, membership: null);

        // Act
        var result = await harness.Service.LoginAsync("person@carepath.local", "ValidPassword1", "corr-2");

        // Assert
        result.Should().Be(LoginResult.Failed);
        harness.Audit.Entries.Should().ContainSingle(entry =>
            entry.Action == PlatformAuditAction.LoginDeniedNoMembership && entry.OrganizationId == OrganizationId && entry.ActorPlatformUserId == UserId);
        harness.IssuedRefresh.Should().BeNull();
    }

    [Fact]
    public async Task Login_WhenMembershipIsInactive_Fails()
    {
        // Arrange
        var harness = new Harness(OrganizationResolutionKind.Tenant, Membership(UserRole.Caregiver, MembershipStatus.Inactive));

        // Act
        var result = await harness.Service.LoginAsync("person@carepath.local", "ValidPassword1", null);

        // Assert
        result.Succeeded.Should().BeFalse();
        harness.Audit.Entries.Should().ContainSingle(entry => entry.Action == PlatformAuditAction.LoginDeniedInactiveMembership);
    }

    [Fact]
    public async Task Login_WhenTheMembershipsTenantUserNoLongerExistsOrIsInactive_Fails()
    {
        // Arrange — a provisioning whose tenant transaction rolled back, or a deactivated profile.
        var missing = new Harness(OrganizationResolutionKind.Tenant, Membership(UserRole.Caregiver, MembershipStatus.Active), tenantUserPresent: false);
        var inactive = new Harness(OrganizationResolutionKind.Tenant, Membership(UserRole.Caregiver, MembershipStatus.Active));
        inactive.TenantUser.IsActive = false;

        // Act
        var missingResult = await missing.Service.LoginAsync("person@carepath.local", "ValidPassword1", null);
        var inactiveResult = await inactive.Service.LoginAsync("person@carepath.local", "ValidPassword1", null);

        // Assert
        missingResult.Succeeded.Should().BeFalse();
        inactiveResult.Succeeded.Should().BeFalse();
        missing.IssuedRefresh.Should().BeNull();
        missing.Audit.Entries.Should().ContainSingle(entry => entry.Action == PlatformAuditAction.LoginDeniedInactiveMembership);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreInvalid_FailsWithoutTouchingMemberships()
    {
        // Arrange
        var harness = new Harness(OrganizationResolutionKind.Tenant, Membership(UserRole.Admin, MembershipStatus.Active), validCredentials: false);

        // Act
        var result = await harness.Service.LoginAsync("person@carepath.local", "wrong", null);

        // Assert
        result.Succeeded.Should().BeFalse();
        harness.Audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Login_AtPlatformHost_RequiresPlatformAdminAndIssuesPlatformSession()
    {
        // Arrange
        var denied = new Harness(OrganizationResolutionKind.Platform, membership: null, isPlatformAdmin: false);
        var allowed = new Harness(OrganizationResolutionKind.Platform, membership: null, isPlatformAdmin: true);

        // Act
        var deniedResult = await denied.Service.LoginAsync("ops@carepath.local", "ValidPassword1", null);
        var allowedResult = await allowed.Service.LoginAsync("ops@carepath.local", "ValidPassword1", null);

        // Assert
        deniedResult.Succeeded.Should().BeFalse();
        denied.Audit.Entries.Should().ContainSingle(entry => entry.Action == PlatformAuditAction.LoginDeniedNotPlatformAdmin);
        allowedResult.Succeeded.Should().BeTrue();
        allowedResult.Token!.TokenKind.Should().Be(ContractAuthTokenKind.Platform);
        allowedResult.Token.Role.Should().BeNull();
        allowedResult.Token.OrganizationId.Should().BeNull();
        allowed.LastJwtRequest!.Roles.Should().Equal(ApplicationRoles.PlatformAdmin);
        allowed.LastJwtRequest.TokenKind.Should().Be(CarePathClaimTypes.PlatformTokenKind);
        allowed.IssuedRefresh.Should().Be((UserId, (Guid?)null, (Guid?)null));
    }

    [Fact]
    public async Task Login_WhenHostIsUnresolved_FailsBeforeCheckingCredentials()
    {
        // Arrange
        var harness = new Harness(OrganizationResolutionKind.Unresolved, membership: null);

        // Act
        var result = await harness.Service.LoginAsync("person@carepath.local", "ValidPassword1", null);

        // Assert
        result.Succeeded.Should().BeFalse();
        harness.CredentialChecks.Should().Be(0);
    }

    [Fact]
    public async Task Refresh_WhenRotationIsBoundToAnotherOrganization_Fails()
    {
        // Arrange
        var membership = Membership(UserRole.Clinician, MembershipStatus.Active);
        var harness = new Harness(OrganizationResolutionKind.Tenant, membership, rotationOrganizationId: Guid.NewGuid());

        // Act
        var result = await harness.Service.RefreshAsync("refresh", null);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_WhenMembershipWasDeactivatedSinceIssue_Fails()
    {
        // Arrange
        var membership = Membership(UserRole.Clinician, MembershipStatus.Inactive);
        var harness = new Harness(OrganizationResolutionKind.Tenant, membership, rotationOrganizationId: OrganizationId);

        // Act
        var result = await harness.Service.RefreshAsync("refresh", null);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_WhenTheMembershipsTenantUserIsInactive_FailsLikeLogin()
    {
        // Arrange — same admissibility rule on both paths: the data plane decides who is active.
        var membership = Membership(UserRole.Clinician, MembershipStatus.Active);
        var harness = new Harness(OrganizationResolutionKind.Tenant, membership, rotationOrganizationId: OrganizationId);
        harness.TenantUser.IsActive = false;

        // Act
        var result = await harness.Service.RefreshAsync("refresh", null);

        // Assert
        result.Succeeded.Should().BeFalse();
        harness.Audit.Entries.Should().Contain(entry => entry.Action == PlatformAuditAction.LoginDeniedInactiveMembership);
    }

    [Fact]
    public async Task Refresh_WhenRotationSucceedsAtTheIssuingOrganization_IssuesANewTenantSession()
    {
        // Arrange
        var membership = Membership(UserRole.Clinician, MembershipStatus.Active);
        var harness = new Harness(OrganizationResolutionKind.Tenant, membership, rotationOrganizationId: OrganizationId);

        // Act
        var result = await harness.Service.RefreshAsync("refresh", null);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Token!.RefreshToken.Should().Be("rotated");
        result.Token.Role.Should().Be(ContractUserRole.Clinician);
    }

    private static OrganizationMembership Membership(UserRole role, MembershipStatus status) => new()
    {
        OrganizationId = OrganizationId,
        PlatformUserId = UserId,
        TenantUserId = UserId,
        Role = role,
        Status = status,
    };

    private sealed class Harness
    {
        public Harness(
            OrganizationResolutionKind kind,
            OrganizationMembership? membership,
            bool validCredentials = true,
            bool isPlatformAdmin = false,
            Guid? rotationOrganizationId = null,
            bool tenantUserPresent = true)
        {
            var context = new Mock<IOrganizationContext>();
            context.SetupGet(c => c.Kind).Returns(kind);
            context.SetupGet(c => c.OrganizationId).Returns(kind == OrganizationResolutionKind.Tenant ? OrganizationId : null);
            context.SetupGet(c => c.Slug).Returns(kind == OrganizationResolutionKind.Tenant ? "brightcare" : null);
            context.SetupGet(c => c.DisplayName).Returns(kind == OrganizationResolutionKind.Tenant ? "BrightCare" : null);

            var user = new IdentityUserResult(true, UserId, "person@carepath.local", "Test Person", isPlatformAdmin, null);
            var identity = new Mock<IIdentityService>();
            identity
                .Setup(service => service.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    CredentialChecks++;
                    return validCredentials ? user : IdentityUserResult.Failed("InvalidCredentials");
                });
            identity
                .Setup(service => service.IssueRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, Guid? membershipId, Guid? organizationId, CancellationToken _) =>
                {
                    IssuedRefresh = (id, membershipId, organizationId);
                    return "refresh";
                });
            identity
                .Setup(service => service.RotateRefreshTokenAsync(It.IsAny<string>(), It.IsAny<RefreshTokenBinding>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RefreshTokenRotationResult(true, user, "rotated", membership?.Id, rotationOrganizationId, null));

            var memberships = new Mock<IRepository<OrganizationMembership>>();
            memberships
                .Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<OrganizationMembership, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(membership is null ? [] : [membership]);
            memberships
                .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => membership is not null && membership.Id == id ? membership : null);
            memberships.Setup(repository => repository.UpdateAsync(It.IsAny<OrganizationMembership>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var controlPlane = new Mock<IControlPlaneUnitOfWork>();
            controlPlane.SetupGet(unitOfWork => unitOfWork.Memberships).Returns(memberships.Object);
            controlPlane.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            TenantUser = new User { Id = UserId, FirstName = "Test", LastName = "Person", Email = "person@carepath.local", PhoneNumber = "555-0100", Role = UserRole.Coordinator };
            var users = new Mock<IRepository<User>>();
            users.Setup(repository => repository.GetByIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(() => tenantUserPresent ? TenantUser : null);
            users.Setup(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var tenant = new Mock<IUnitOfWork>();
            tenant.SetupGet(unitOfWork => unitOfWork.Users).Returns(users.Object);
            tenant.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var jwt = new Mock<IJwtTokenService>();
            jwt
                .Setup(service => service.CreateTokenAsync(It.IsAny<JwtTokenRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((JwtTokenRequest request, CancellationToken _) =>
                {
                    LastJwtRequest = request;
                    return new JwtTokenResult("access", DateTime.UtcNow.AddMinutes(30));
                });

            Audit = new RecordingAuditLogger();
            Service = new LoginService(context.Object, identity.Object, controlPlane.Object, tenant.Object, jwt.Object, Audit);
        }

        public LoginService Service { get; }
        public RecordingAuditLogger Audit { get; }
        public JwtTokenRequest? LastJwtRequest { get; private set; }
        public (Guid, Guid?, Guid?)? IssuedRefresh { get; private set; }
        public int CredentialChecks { get; private set; }
        public User TenantUser { get; }
    }

    private sealed class RecordingAuditLogger : IPlatformAuditLogger
    {
        public List<PlatformAuditEntry> Entries { get; } = [];

        public Task LogAsync(PlatformAuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
