using System.Linq.Expressions;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Organization.Services;
using CarePath.Contracts.Common;
using CarePath.Contracts.Organization;
using CarePath.Domain.Entities.Identity;
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

    [Fact]
    public async Task List_ReturnsOnlyTheCurrentOrganizationsMembershipsWithDirectoryFields()
    {
        // Arrange
        var membership = Membership(UserRole.Caregiver);
        var harness = new Harness(membership, activeAdmins: 2);

        // Act
        var page = await harness.Service.ListAsync(new PagedRequest());

        // Assert
        page.TotalCount.Should().Be(1);
        var row = page.Items.Single();
        row.Id.Should().Be(membership.Id);
        row.DisplayName.Should().Be("Directory Person");
        row.Email.Should().EndWith("@carepath.local");
        row.Role.Should().Be(CarePath.Contracts.Enumerations.UserRole.Caregiver);
    }

    [Fact]
    public async Task Create_WhenEmailIsNew_ProvisionsIdentityThenCreatesTenantProfileWithTheSameId()
    {
        // Arrange
        var harness = new Harness(membership: null, activeAdmins: 2);

        // Act
        var dto = await harness.Service.CreateAsync(new CreateMembershipRequest
        {
            Email = "new@carepath.local",
            Role = CarePath.Contracts.Enumerations.UserRole.Coordinator,
            FirstName = "New",
            LastName = "Person",
            PhoneNumber = "555-0100",
            TemporaryPassword = "ValidPassword1",
        });

        // Assert
        harness.TenantUser.Should().NotBeNull();
        harness.ProvisionedRequest!.UserId.Should().Be(harness.TenantUser!.Id);
        harness.TenantUser.PlatformUserId.Should().Be(harness.TenantUser.Id);
        harness.ProvisionedRequest.DisplayName.Should().Be("New Person");
        dto.PlatformUserId.Should().Be(harness.TenantUser.Id);
        dto.Role.Should().Be(CarePath.Contracts.Enumerations.UserRole.Coordinator);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_WhenNoPasswordIsGiven_IsRejectedBeforeAnyWriteWhetherOrNotTheIdentityExists(bool identityExists)
    {
        // Arrange — enumeration safety: the password rule is identical for new and existing identities.
        var harness = new Harness(membership: null, activeAdmins: 2);
        if (identityExists)
        {
            harness.ExistingIdentity = new PlatformUserSummary(Guid.NewGuid(), "new@carepath.local", "Elsewhere Person");
        }

        // Act
        var act = async () => await harness.Service.CreateAsync(new CreateMembershipRequest
        {
            Email = "new@carepath.local",
            Role = CarePath.Contracts.Enumerations.UserRole.Coordinator,
            FirstName = "New",
            LastName = "Person",
            PhoneNumber = "555-0100",
        });

        // Assert
        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        harness.TenantUser.Should().BeNull();
        harness.Added.Should().BeNull();
        harness.ProvisionedRequest.Should().BeNull();
    }

    [Fact]
    public async Task Create_ResponseIsBuiltFromTheRequestWhetherOrNotTheIdentityExists()
    {
        // Arrange — enumeration safety: an existing identity's directory name must never leak.
        var fresh = new Harness(membership: null, activeAdmins: 2);
        var linked = new Harness(membership: null, activeAdmins: 2)
        {
            ExistingIdentity = new PlatformUserSummary(Guid.NewGuid(), "shared@carepath.local", "Directory Name That Must Not Leak"),
        };
        CreateMembershipRequest Request(string email) => new()
        {
            Email = email,
            Role = CarePath.Contracts.Enumerations.UserRole.Caregiver,
            FirstName = "Req",
            LastName = "Name",
            PhoneNumber = "555-0100",
            TemporaryPassword = "ValidPassword1",
        };

        // Act
        var freshDto = await fresh.Service.CreateAsync(Request("new@carepath.local"));
        var linkedDto = await linked.Service.CreateAsync(Request("shared@carepath.local"));

        // Assert
        freshDto.DisplayName.Should().Be("Req Name");
        linkedDto.DisplayName.Should().Be("Req Name");
        linkedDto.Email.Should().Be("shared@carepath.local");
        linkedDto.Status.Should().Be(freshDto.Status);
    }

    [Fact]
    public async Task Create_WhenEmailBelongsToAnExistingIdentity_LinksItWithoutProvisioning()
    {
        // Arrange — a caregiver who already works for another agency.
        var harness = new Harness(membership: null, activeAdmins: 2);
        var existingId = Guid.NewGuid();
        harness.ExistingIdentity = new PlatformUserSummary(existingId, "shared@carepath.local", "Shared Person");

        // Act
        var dto = await harness.Service.CreateAsync(new CreateMembershipRequest
        {
            Email = "shared@carepath.local",
            Role = CarePath.Contracts.Enumerations.UserRole.Caregiver,
            FirstName = "Shared",
            LastName = "Person",
            PhoneNumber = "555-0100",
            TemporaryPassword = "IgnoredPassword1",
        });

        // Assert
        harness.ProvisionedRequest.Should().BeNull();
        harness.Added!.PlatformUserId.Should().Be(existingId);
        harness.Added.TenantUserId.Should().Be(existingId);
        harness.TenantUser!.Id.Should().Be(existingId);
        dto.Email.Should().Be("shared@carepath.local");
        harness.Audit.Entries.Should().ContainSingle(entry => entry.Action == PlatformAuditAction.MembershipCreated);
    }

    [Fact]
    public async Task Create_WhenProvisioningFails_WritesNoTenantProfile()
    {
        // Arrange — control plane first: nothing reaches the tenant database on a phase-1 failure.
        var harness = new Harness(membership: null, activeAdmins: 2);
        harness.Provisioning
            .Setup(provisioning => provisioning.ProvisionUserAsync(It.IsAny<IdentityProvisioningRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityProvisioningResult.Failed("IdentityProvisioningFailed"));

        // Act
        var act = async () => await harness.Service.CreateAsync(new CreateMembershipRequest
        {
            Email = "new@carepath.local",
            Role = CarePath.Contracts.Enumerations.UserRole.Coordinator,
            FirstName = "New",
            LastName = "Person",
            PhoneNumber = "555-0100",
            TemporaryPassword = "ValidPassword1",
        });

        // Assert
        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        harness.TenantUser.Should().BeNull();
    }

    [Fact]
    public async Task Create_WhenTheTenantWriteFailsForANewIdentity_CompensatesThroughProvisioningAndRethrows()
    {
        // Arrange
        var harness = new Harness(membership: null, activeAdmins: 2);
        harness.TenantUsers
            .Setup(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("tenant down"));

        // Act
        var act = async () => await harness.Service.CreateAsync(new CreateMembershipRequest
        {
            Email = "new@carepath.local",
            Role = CarePath.Contracts.Enumerations.UserRole.Coordinator,
            FirstName = "New",
            LastName = "Person",
            PhoneNumber = "555-0100",
            TemporaryPassword = "ValidPassword1",
        });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("tenant down");
        harness.Provisioning.Verify(provisioning => provisioning.CompensateAsync(harness.ProvisionedRequest!.UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenTheTenantWriteFailsForAnExistingIdentity_SoftDeletesTheMembershipEvenIfCompensationFails()
    {
        // Arrange — the original failure must propagate, never the compensation's.
        var harness = new Harness(membership: null, activeAdmins: 2)
        {
            ExistingIdentity = new PlatformUserSummary(Guid.NewGuid(), "shared@carepath.local", "Shared Person"),
        };
        harness.TenantUsers
            .Setup(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("tenant down"));
        harness.MembershipsRepository
            .Setup(repository => repository.DeleteAsync(It.IsAny<OrganizationMembership>(), It.IsAny<CancellationToken>()))
            .Returns((OrganizationMembership membership, CancellationToken _) => { membership.IsDeleted = true; throw new TimeoutException("compensation failed"); });

        // Act
        var act = async () => await harness.Service.CreateAsync(new CreateMembershipRequest
        {
            Email = "shared@carepath.local",
            Role = CarePath.Contracts.Enumerations.UserRole.Caregiver,
            FirstName = "Shared",
            LastName = "Person",
            PhoneNumber = "555-0100",
            TemporaryPassword = "IgnoredPassword1",
        });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("tenant down");
        harness.Added!.IsDeleted.Should().BeTrue();
        harness.Audit.Entries.Should().NotContain(entry => entry.Action == PlatformAuditAction.MembershipCreated);
    }

    [Fact]
    public async Task Create_WhenTheTenantProfileAlreadyExists_DoesNotTouchOrDeleteIt()
    {
        // Arrange — shared development database: the profile row pre-exists with the identity's id.
        var existingId = Guid.NewGuid();
        var harness = new Harness(membership: null, activeAdmins: 2)
        {
            ExistingIdentity = new PlatformUserSummary(existingId, "shared@carepath.local", "Shared Person"),
            TenantUser = new User { Id = existingId, FirstName = "Pre", LastName = "Existing", Email = "shared@carepath.local", PhoneNumber = "555", Role = UserRole.Caregiver },
        };

        // Act
        await harness.Service.CreateAsync(new CreateMembershipRequest
        {
            Email = "shared@carepath.local",
            Role = CarePath.Contracts.Enumerations.UserRole.Caregiver,
            FirstName = "Other",
            LastName = "Name",
            PhoneNumber = "555-0100",
            TemporaryPassword = "IgnoredPassword1",
        });

        // Assert
        harness.TenantUser!.FirstName.Should().Be("Pre");
        harness.TenantUser.IsDeleted.Should().BeFalse();
        harness.TenantUsers.Verify(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangeRoleById_WhenRoleIsUndefined_IsRejectedBeforeAnyWrite()
    {
        // Arrange
        var membership = Membership(UserRole.Caregiver);
        var harness = new Harness(membership, activeAdmins: 2);

        // Act
        var act = async () => await harness.Service.ChangeRoleAsync(membership.Id, new UpdateMembershipRoleRequest { Role = (CarePath.Contracts.Enumerations.UserRole)999 });

        // Assert
        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        membership.Role.Should().Be(UserRole.Caregiver);
        harness.Saves.Should().Be(0);
    }

    [Fact]
    public async Task ChangeRoleById_WhenTheTenantMirrorFails_RevertsTheMembershipAndRethrows()
    {
        // Arrange — D-CP04-09: membership and tenant mirror never disagree.
        var membership = Membership(UserRole.Caregiver);
        membership.TenantUserId = membership.PlatformUserId;
        var harness = new Harness(membership, activeAdmins: 2)
        {
            TenantUser = new User { Id = membership.PlatformUserId, FirstName = "T", LastName = "U", Email = "t@carepath.local", PhoneNumber = "555", Role = UserRole.Caregiver },
        };
        harness.TenantUsers
            .Setup(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("tenant down"));

        // Act
        var act = async () => await harness.Service.ChangeRoleAsync(membership.Id, new UpdateMembershipRoleRequest { Role = CarePath.Contracts.Enumerations.UserRole.Coordinator });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        membership.Role.Should().Be(UserRole.Caregiver);
        membership.SecurityVersion.Should().Be(2);
    }

    [Fact]
    public async Task Create_WhenAMembershipAlreadyExists_Conflicts()
    {
        // Arrange
        var membership = Membership(UserRole.Caregiver);
        var harness = new Harness(membership, activeAdmins: 2);
        harness.ExistingIdentity = new PlatformUserSummary(membership.PlatformUserId, "member@carepath.local", "Member");

        // Act
        var act = async () => await harness.Service.CreateAsync(new CreateMembershipRequest
        {
            Email = "member@carepath.local",
            Role = CarePath.Contracts.Enumerations.UserRole.Caregiver,
            FirstName = "Member",
            LastName = "Person",
            PhoneNumber = "555-0100",
            TemporaryPassword = "IgnoredPassword1",
        });

        // Assert
        (await act.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be("membership.exists");
    }

    [Fact]
    public async Task ChangeRoleById_MirrorsTheTenantProfileAndReturnsTheDto()
    {
        // Arrange
        var membership = Membership(UserRole.Caregiver);
        membership.TenantUserId = membership.PlatformUserId;
        var harness = new Harness(membership, activeAdmins: 2)
        {
            TenantUser = new User { Id = membership.PlatformUserId, FirstName = "T", LastName = "U", Email = "t@carepath.local", PhoneNumber = "555", Role = UserRole.Caregiver },
        };

        // Act
        var dto = await harness.Service.ChangeRoleAsync(membership.Id, new UpdateMembershipRoleRequest { Role = CarePath.Contracts.Enumerations.UserRole.Coordinator });

        // Assert
        dto.Role.Should().Be(CarePath.Contracts.Enumerations.UserRole.Coordinator);
        harness.TenantUser!.Role.Should().Be(UserRole.Coordinator);
        harness.TenantUsers.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetStatusById_WhenMembershipBelongsToAnotherOrganization_IsNotFound()
    {
        // Arrange
        var membership = Membership(UserRole.Caregiver);
        membership.OrganizationId = Guid.NewGuid();
        var harness = new Harness(membership, activeAdmins: 2);

        // Act
        var act = async () => await harness.Service.SetStatusAsync(membership.Id, new UpdateMembershipStatusRequest { IsActive = false });

        // Assert
        await act.Should().ThrowAsync<ResourceNotFoundException>();
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
            MembershipsRepository = memberships;
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

            memberships
                .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => membership is not null && membership.Id == id ? membership : null);
            memberships
                .Setup(repository => repository.ExistsAsync(It.IsAny<Expression<Func<OrganizationMembership, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<OrganizationMembership, bool>> predicate, CancellationToken _) =>
                    membership is not null && predicate.Compile()(membership));
            memberships
                .Setup(repository => repository.GetPagedAsync(It.IsAny<Expression<Func<OrganizationMembership, bool>>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<OrganizationMembership, bool>> predicate, int _, int _, CancellationToken _) =>
                {
                    var rows = membership is null ? new List<OrganizationMembership>() : new[] { membership }.Where(predicate.Compile()).ToList();
                    return ((IReadOnlyList<OrganizationMembership>)rows, rows.Count);
                });
            memberships
                .Setup(repository => repository.AddAsync(It.IsAny<OrganizationMembership>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((OrganizationMembership added, CancellationToken _) => { Added = added; membership ??= added; return added; });
            memberships
                .Setup(repository => repository.DeleteAsync(It.IsAny<OrganizationMembership>(), It.IsAny<CancellationToken>()))
                .Returns((OrganizationMembership deleted, CancellationToken _) => { deleted.IsDeleted = true; return Task.CompletedTask; });

            TenantUsers = new Mock<IRepository<User>>();
            TenantUsers.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => TenantUser is not null && TenantUser.Id == id ? TenantUser : null);
            TenantUsers.Setup(repository => repository.ExistsAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            TenantUsers.Setup(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((User user, CancellationToken _) => { TenantUser = user; return user; });
            TenantUsers.Setup(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            TenantUsers.Setup(repository => repository.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .Returns((User user, CancellationToken _) => { user.IsDeleted = true; return Task.CompletedTask; });
            var tenant = new Mock<IUnitOfWork>();
            tenant.SetupGet(unitOfWork => unitOfWork.Users).Returns(TenantUsers.Object);
            tenant.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            Directory = new Mock<IPlatformUserDirectory>();
            Directory.Setup(directory => directory.GetSummariesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                    (IReadOnlyDictionary<Guid, PlatformUserSummary>)ids.ToDictionary(id => id, id => new PlatformUserSummary(id, $"{id:N}@carepath.local", "Directory Person")));
            Directory.Setup(directory => directory.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string email, CancellationToken _) => ExistingIdentity is not null && ExistingIdentity.Email == email ? ExistingIdentity : null);

            Provisioning = new Mock<IIdentityProvisioningService>();
            Provisioning.Setup(provisioning => provisioning.ProvisionUserAsync(It.IsAny<IdentityProvisioningRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IdentityProvisioningRequest request, CancellationToken _) =>
                {
                    ProvisionedRequest = request;
                    membership ??= new OrganizationMembership { OrganizationId = OrganizationId, PlatformUserId = request.UserId, TenantUserId = request.UserId, Role = Enum.Parse<UserRole>(request.Role) };
                    return IdentityProvisioningResult.Success();
                });

            Audit = new RecordingAuditLogger();
            Service = new OrganizationMembershipService(context.Object, currentUser.Object, controlPlane.Object, Audit, tenant.Object, Directory.Object, Provisioning.Object);
        }

        public OrganizationMembershipService Service { get; }
        public RecordingAuditLogger Audit { get; }
        public int Saves { get; private set; }
        public Mock<IRepository<User>> TenantUsers { get; }
        public Mock<IRepository<OrganizationMembership>> MembershipsRepository { get; }
        public Mock<IPlatformUserDirectory> Directory { get; }
        public Mock<IIdentityProvisioningService> Provisioning { get; }
        public User? TenantUser { get; set; }
        public PlatformUserSummary? ExistingIdentity { get; set; }
        public OrganizationMembership? Added { get; private set; }
        public IdentityProvisioningRequest? ProvisionedRequest { get; private set; }
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
