using System.Linq.Expressions;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Organization.Services;
using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentAssertions;
using Moq;

namespace CarePath.Application.Tests.Organization;

public sealed class OrganizationMembershipServiceTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();

    [Fact]
    public async Task ChangeRole_WhenMembershipExists_UpdatesRoleBumpsSecurityVersionAndAudits()
    {
        // Arrange
        var membership = Membership(UserRole.Caregiver);
        var harness = new Harness(membership, activeAdmins: 2);

        // Act
        await harness.Service.ChangeRoleForTenantUserAsync(membership.PlatformUserId, UserRole.Coordinator);

        // Assert
        membership.Role.Should().Be(UserRole.Coordinator);
        membership.SecurityVersion.Should().Be(1);
        harness.Saves.Should().Be(1);
        harness.Audit.Entries.Should().ContainSingle(entry => entry.Action == PlatformAuditAction.MembershipRoleChanged && entry.EntityId == membership.Id);
    }

    [Fact]
    public async Task ChangeRole_WhenRoleIsUnchanged_DoesNothing()
    {
        // Arrange
        var membership = Membership(UserRole.Caregiver);
        var harness = new Harness(membership, activeAdmins: 2);

        // Act
        await harness.Service.ChangeRoleForTenantUserAsync(membership.PlatformUserId, UserRole.Caregiver);

        // Assert
        membership.SecurityVersion.Should().Be(0);
        harness.Saves.Should().Be(0);
    }

    [Fact]
    public async Task ChangeRole_WhenDemotingTheLastActiveAdmin_IsRejected()
    {
        // Arrange
        var membership = Membership(UserRole.Admin);
        var harness = new Harness(membership, activeAdmins: 1);

        // Act
        var act = async () => await harness.Service.ChangeRoleForTenantUserAsync(membership.PlatformUserId, UserRole.Coordinator);

        // Assert
        (await act.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be("membership.last_active_admin");
        membership.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task SetStatus_WhenDeactivatingTheLastActiveAdmin_IsRejected()
    {
        // Arrange
        var membership = Membership(UserRole.Admin);
        var harness = new Harness(membership, activeAdmins: 1);

        // Act
        var act = async () => await harness.Service.SetStatusForTenantUserAsync(membership.PlatformUserId, active: false);

        // Assert
        await act.Should().ThrowAsync<ResourceConflictException>();
        membership.Status.Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public async Task SetStatus_WhenDeactivatingARegularMember_DeactivatesAndAudits()
    {
        // Arrange
        var membership = Membership(UserRole.Caregiver);
        var harness = new Harness(membership, activeAdmins: 2);

        // Act
        await harness.Service.SetStatusForTenantUserAsync(membership.PlatformUserId, active: false);

        // Assert
        membership.Status.Should().Be(MembershipStatus.Inactive);
        membership.SecurityVersion.Should().Be(1);
        harness.Audit.Entries.Should().ContainSingle(entry => entry.Action == PlatformAuditAction.MembershipDeactivated);
    }

    [Fact]
    public async Task ChangeRole_WhenNoMembershipExistsInThisOrganization_ThrowsNonPhiNotFound()
    {
        // Arrange
        var harness = new Harness(membership: null, activeAdmins: 2);

        // Act
        var act = async () => await harness.Service.ChangeRoleForTenantUserAsync(Guid.NewGuid(), UserRole.Coordinator);

        // Assert
        (await act.Should().ThrowAsync<ResourceNotFoundException>()).Which.IsPhiResource.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeRole_WhenHostIsNotATenant_IsDenied()
    {
        // Arrange
        var harness = new Harness(Membership(UserRole.Caregiver), activeAdmins: 2, kind: OrganizationResolutionKind.Platform);

        // Act
        var act = async () => await harness.Service.ChangeRoleForTenantUserAsync(Guid.NewGuid(), UserRole.Coordinator);

        // Assert
        await act.Should().ThrowAsync<ResourceAccessDeniedException>();
    }

    private static OrganizationMembership Membership(UserRole role) => new()
    {
        OrganizationId = OrganizationId,
        PlatformUserId = Guid.NewGuid(),
        Role = role,
        Status = MembershipStatus.Active,
    };

    private sealed class Harness
    {
        public Harness(OrganizationMembership? membership, int activeAdmins, OrganizationResolutionKind kind = OrganizationResolutionKind.Tenant)
        {
            var context = new Mock<IOrganizationContext>();
            context.SetupGet(c => c.Kind).Returns(kind);
            context.SetupGet(c => c.OrganizationId).Returns(kind == OrganizationResolutionKind.Tenant ? OrganizationId : null);

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
            currentUser.SetupGet(c => c.CorrelationId).Returns("corr");

            var memberships = new Mock<IRepository<OrganizationMembership>>();
            memberships
                .Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<OrganizationMembership, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<OrganizationMembership, bool>> predicate, CancellationToken _) =>
                    membership is null ? [] : new[] { membership }.Where(predicate.Compile()).ToList());
            memberships
                .Setup(repository => repository.CountAsync(It.IsAny<Expression<Func<OrganizationMembership, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(activeAdmins);
            memberships.Setup(repository => repository.UpdateAsync(It.IsAny<OrganizationMembership>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var controlPlane = new Mock<IControlPlaneUnitOfWork>();
            controlPlane.SetupGet(unitOfWork => unitOfWork.Memberships).Returns(memberships.Object);
            controlPlane.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => { Saves++; return 1; });

            Audit = new RecordingAuditLogger();
            Service = new OrganizationMembershipService(context.Object, currentUser.Object, controlPlane.Object, Audit);
        }

        public OrganizationMembershipService Service { get; }
        public RecordingAuditLogger Audit { get; }
        public int Saves { get; private set; }
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
