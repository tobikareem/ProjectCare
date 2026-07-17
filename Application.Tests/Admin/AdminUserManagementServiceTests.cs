using System.Linq.Expressions;
using System.Data;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Admin.Services;
using CarePath.Application.Common.Exceptions;
using CarePath.Contracts.Admin;
using CarePath.Contracts.Common;
using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Entities.Transitions;
using CarePath.Domain.Interfaces.Repositories;
using FluentAssertions;
using FluentValidation;
using Moq;
using DomainClient = global::CarePath.Domain.Entities.Identity.Client;
using ContractUserRole = CarePath.Contracts.Enumerations.UserRole;
using DomainUserRole = CarePath.Domain.Enumerations.UserRole;

namespace CarePath.Application.Tests.Admin;

public sealed class AdminUserManagementServiceTests
{
    [Fact]
    public async Task UpdateRoleAsync_WhenActorTokenIsStaleAndActorIsNoLongerAdmin_DeniesAccess()
    {
        var actorId = Guid.NewGuid();
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(DomainUserRole.Coordinator, actorId));
        var roleManagement = new Mock<IIdentityRoleManagementService>();
        var service = CreateService(unitOfWork, actorId, roleManagement: roleManagement);

        var act = async () => await service.UpdateRoleAsync(
            Guid.NewGuid(),
            new UpdateUserRoleRequest { Role = ContractUserRole.Admin });

        await act.Should().ThrowAsync<ResourceAccessDeniedException>()
            .Where(exception => !exception.IsPhiResource && exception.ReasonCode == "RoleInsufficient");
        roleManagement.Verify(
            service => service.ReplaceUserRoleAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenTargetIsLastActiveAdmin_DeactivationIsRejected()
    {
        var actorId = Guid.NewGuid();
        var admin = User(DomainUserRole.Admin, actorId);
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(admin);
        unitOfWork.Users.Setup(repository => repository.CountAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var service = CreateService(unitOfWork, actorId);

        var act = async () => await service.UpdateStatusAsync(
            admin.Id,
            new UpdateUserStatusRequest { IsActive = false });

        await act.Should().ThrowAsync<ResourceConflictException>()
            .Where(exception => exception.Code == "admin.last_active_admin");
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenTargetHasClientProfileAndNewRoleIsNotClient_IsRejected()
    {
        var actorId = Guid.NewGuid();
        var target = User(DomainUserRole.Client);
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(DomainUserRole.Admin, actorId));
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        unitOfWork.Caregivers.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<Caregiver, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        unitOfWork.Clients.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<DomainClient, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService(unitOfWork, actorId);

        var act = async () => await service.UpdateRoleAsync(
            target.Id,
            new UpdateUserRoleRequest { Role = ContractUserRole.Coordinator });

        await act.Should().ThrowAsync<ResourceConflictException>()
            .Where(exception => exception.Code == "admin.profile_role_coupled");
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenRequestIsValid_UpdatesDomainRoleAndIdentityRole()
    {
        var actorId = Guid.NewGuid();
        var target = User(DomainUserRole.Coordinator);
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(DomainUserRole.Admin, actorId));
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        unitOfWork.Users.Setup(repository => repository.UpdateAsync(target, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unitOfWork.Caregivers.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<Caregiver, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        unitOfWork.Clients.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<DomainClient, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var roleManagement = new Mock<IIdentityRoleManagementService>();
        roleManagement.Setup(service => service.ReplaceUserRoleAsync(target.Id, DomainUserRole.Clinician.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var auditLogger = new Mock<IPhiAuditLogger>();
        auditLogger.Setup(logger => logger.LogAsync(It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(unitOfWork, actorId, roleManagement, auditLogger);

        var result = await service.UpdateRoleAsync(
            target.Id,
            new UpdateUserRoleRequest { Role = ContractUserRole.Clinician });

        result.Role.Should().Be(ContractUserRole.Clinician);
        target.Role.Should().Be(DomainUserRole.Clinician);
        unitOfWork.Mock.Verify(
            work => work.ExecuteInTransactionAsync(
                IsolationLevel.Serializable,
                It.IsAny<Func<CancellationToken, Task<UserAccountDto>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        roleManagement.VerifyAll();
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry =>
                entry.Action == AuditAction.RoleChanged &&
                entry.EntityType == ProtectedResourceType.UserAccount &&
                entry.EntityId == target.Id &&
                entry.Attributes != null &&
                entry.Attributes["OldRole"] == DomainUserRole.Coordinator.ToString() &&
                entry.Attributes["NewRole"] == DomainUserRole.Clinician.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenRoleValueIsInvalid_RejectsBeforeIdentitySync()
    {
        var actorId = Guid.NewGuid();
        var unitOfWork = CreateUnitOfWork();
        var roleManagement = new Mock<IIdentityRoleManagementService>();
        var service = CreateService(unitOfWork, actorId, roleManagement: roleManagement);

        var act = async () => await service.UpdateRoleAsync(
            Guid.NewGuid(),
            new UpdateUserRoleRequest { Role = (ContractUserRole)999 });

        await act.Should().ThrowAsync<ValidationException>();
        roleManagement.Verify(
            service => service.ReplaceUserRoleAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        unitOfWork.Mock.Verify(
            work => work.ExecuteInTransactionAsync(
                It.IsAny<IsolationLevel>(),
                It.IsAny<Func<CancellationToken, Task<UserAccountDto>>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateStaffUserAsync_WhenRoleIsClient_RejectsStaffProvisioning()
    {
        var actorId = Guid.NewGuid();
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(DomainUserRole.Admin, actorId));
        var identityProvisioning = new Mock<IIdentityProvisioningService>();
        var service = new AdminUserManagementService(
            unitOfWork,
            new TestCurrentUserContext(actorId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Admin }),
            identityProvisioning.Object,
            Mock.Of<IIdentityRoleManagementService>(),
            Mock.Of<IPhiAuditLogger>());

        var act = async () => await service.CreateStaffUserAsync(new CreateStaffUserRequest
        {
            FirstName = "Test",
            LastName = "Client",
            Email = "client@example.test",
            PhoneNumber = "555-0100",
            TemporaryPassword = "ValidPass1",
            Role = ContractUserRole.Client,
        });

        await act.Should().ThrowAsync<ValidationException>();
        identityProvisioning.Verify(
            service => service.ProvisionUserAsync(It.IsAny<IdentityProvisioningRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAvailableRolesAsync_WhenActorIsActiveAdmin_ReturnsAllRoles()
    {
        var actorId = Guid.NewGuid();
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(DomainUserRole.Admin, actorId));
        var service = CreateService(unitOfWork, actorId);

        var roles = await service.GetAvailableRolesAsync();

        roles.Should().BeEquivalentTo(Enum.GetValues<ContractUserRole>());
    }

    [Fact]
    public async Task GetUsersAsync_WhenFiltersProvided_PagesAtRepositoryWithEmailAndDisplayNameSearchOnly()
    {
        var actorId = Guid.NewGuid();
        var matching = User(DomainUserRole.Coordinator, firstName: "Nina", lastName: "Patel", email: "np@example.test");
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(DomainUserRole.Admin, actorId));
        Expression<Func<User, bool>>? capturedPredicate = null;
        Expression<Func<User, string>>? capturedOrderBy = null;
        Expression<Func<User, string>>? capturedThenBy = null;
        unitOfWork.Users.Setup(repository => repository.GetPagedAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Expression<Func<User, string>>>(),
                It.IsAny<Expression<Func<User, string>>>(),
                2,
                10,
                It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<User, bool>>, Expression<Func<User, string>>, Expression<Func<User, string>>?, int, int, CancellationToken>(
                (predicate, orderBy, thenBy, _, _, _) =>
                {
                    capturedPredicate = predicate;
                    capturedOrderBy = orderBy;
                    capturedThenBy = thenBy;
                })
            .ReturnsAsync((new[] { matching }, 11));
        SetupBatchedProfileLookups(unitOfWork, activeAdminCount: 2);
        var service = CreateService(unitOfWork, actorId);

        var result = await service.GetUsersAsync(
            new PagedRequest { PageNumber = 2, PageSize = 10 },
            role: ContractUserRole.Coordinator,
            isActive: true,
            search: "nina");

        result.TotalCount.Should().Be(11);
        result.PageNumber.Should().Be(2);
        result.Items.Should().ContainSingle(item => item.Id == matching.Id);
        var predicate = capturedPredicate!.Compile();
        predicate(matching).Should().BeTrue("display-name search must match");
        predicate(User(DomainUserRole.Coordinator, email: "nina@example.test")).Should().BeTrue("email search must match");
        predicate(User(DomainUserRole.Coordinator, firstName: "Rae", lastName: "Kim", email: "rk@example.test")).Should().BeFalse("non-matching text is excluded");
        predicate(User(DomainUserRole.Clinician, firstName: "Nina", lastName: "Patel", email: "np2@example.test")).Should().BeFalse("the role filter composes with search");
        var inactiveMatch = User(DomainUserRole.Coordinator, firstName: "Nina", lastName: "Patel", email: "np3@example.test");
        inactiveMatch.IsActive = false;
        predicate(inactiveMatch).Should().BeFalse("the isActive filter composes with search");
        var phoneOnlyMatch = User(DomainUserRole.Coordinator, firstName: "Rae", lastName: "Kim", email: "rk2@example.test");
        phoneOnlyMatch.PhoneNumber = "555-nina";
        predicate(phoneOnlyMatch).Should().BeFalse("search must never match phone or any non-approved field");
        capturedOrderBy!.Compile()(matching).Should().Be("Patel", "the list orders by last name first");
        capturedThenBy!.Compile()(matching).Should().Be("Nina", "the list orders by first name second");
        unitOfWork.Users.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<User, bool>>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetUsersAsync_ComputesGuardrailActionFieldsForEachRow()
    {
        var actorId = Guid.NewGuid();
        var lastAdmin = User(DomainUserRole.Admin, firstName: "Ada", lastName: "Admin", email: "aa@example.test");
        var caregiverCoupled = User(DomainUserRole.Caregiver, firstName: "Cara", lastName: "Giver", email: "cg@example.test");
        var plainStaff = User(DomainUserRole.Coordinator, firstName: "Cody", lastName: "Coord", email: "cc@example.test");
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(DomainUserRole.Admin, actorId));
        unitOfWork.Users.Setup(repository => repository.GetPagedAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Expression<Func<User, string>>>(),
                It.IsAny<Expression<Func<User, string>>>(),
                1,
                20,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { lastAdmin, caregiverCoupled, plainStaff }, 3));
        unitOfWork.Caregivers.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Caregiver, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Caregiver { Id = Guid.NewGuid(), UserId = caregiverCoupled.Id } });
        unitOfWork.Clients.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<DomainClient, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DomainClient>());
        unitOfWork.Users.Setup(repository => repository.CountAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var service = CreateService(unitOfWork, actorId);

        var result = await service.GetUsersAsync(new PagedRequest { PageNumber = 1, PageSize = 20 });

        var lastAdminRow = result.Items.Single(item => item.Id == lastAdmin.Id);
        lastAdminRow.CanChangeRole.Should().BeFalse();
        lastAdminRow.CanDeactivate.Should().BeFalse();
        lastAdminRow.DisabledReason.Should().Be("Last active admin");
        var caregiverRow = result.Items.Single(item => item.Id == caregiverCoupled.Id);
        caregiverRow.HasCaregiverProfile.Should().BeTrue();
        caregiverRow.CanChangeRole.Should().BeFalse();
        caregiverRow.CanDeactivate.Should().BeTrue();
        caregiverRow.DisabledReason.Should().Be("Profile role coupled");
        var staffRow = result.Items.Single(item => item.Id == plainStaff.Id);
        staffRow.CanChangeRole.Should().BeTrue();
        staffRow.CanDeactivate.Should().BeTrue();
        staffRow.DisabledReason.Should().BeNull();
    }

    [Fact]
    public async Task GetUsersAsync_WhenActorIsDeactivatedAdmin_DeniesBeforeQuery()
    {
        var actorId = Guid.NewGuid();
        var deactivatedAdmin = User(DomainUserRole.Admin, actorId);
        deactivatedAdmin.IsActive = false;
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deactivatedAdmin);
        var service = CreateService(unitOfWork, actorId);

        var act = async () => await service.GetUsersAsync(new PagedRequest());

        await act.Should().ThrowAsync<ResourceAccessDeniedException>()
            .Where(exception => exception.ReasonCode == "RoleInsufficient");
        unitOfWork.Users.Verify(repository => repository.GetPagedAsync(
            It.IsAny<Expression<Func<User, bool>>>(),
            It.IsAny<Expression<Func<User, string>>>(),
            It.IsAny<Expression<Func<User, string>>>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateStaffUserAsync_WhenActorIsDeactivatedAdmin_DeniesBeforeProvisioning()
    {
        var actorId = Guid.NewGuid();
        var deactivatedAdmin = User(DomainUserRole.Admin, actorId);
        deactivatedAdmin.IsActive = false;
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deactivatedAdmin);
        var identityProvisioning = new Mock<IIdentityProvisioningService>();
        var service = new AdminUserManagementService(
            unitOfWork,
            new TestCurrentUserContext(actorId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Admin }),
            identityProvisioning.Object,
            Mock.Of<IIdentityRoleManagementService>(),
            Mock.Of<IPhiAuditLogger>());

        var act = async () => await service.CreateStaffUserAsync(ValidStaffRequest());

        await act.Should().ThrowAsync<ResourceAccessDeniedException>();
        identityProvisioning.Verify(
            provisioning => provisioning.ProvisionUserAsync(It.IsAny<IdentityProvisioningRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenActorIsNoLongerAdmin_DeniesBeforeTargetLookup()
    {
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(DomainUserRole.Clinician, actorId));
        var service = CreateService(unitOfWork, actorId);

        var act = async () => await service.UpdateStatusAsync(targetId, new UpdateUserStatusRequest { IsActive = false });

        await act.Should().ThrowAsync<ResourceAccessDeniedException>();
        unitOfWork.Users.Verify(repository => repository.GetByIdAsync(targetId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenTargetIsLastActiveAdmin_DemotionIsRejected()
    {
        var actorId = Guid.NewGuid();
        var lastAdmin = User(DomainUserRole.Admin);
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(DomainUserRole.Admin, actorId));
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(lastAdmin.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lastAdmin);
        unitOfWork.Users.Setup(repository => repository.CountAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var roleManagement = new Mock<IIdentityRoleManagementService>();
        var service = CreateService(unitOfWork, actorId, roleManagement: roleManagement);

        var act = async () => await service.UpdateRoleAsync(
            lastAdmin.Id,
            new UpdateUserRoleRequest { Role = ContractUserRole.Coordinator });

        await act.Should().ThrowAsync<ResourceConflictException>()
            .Where(exception => exception.Code == "admin.last_active_admin");
        roleManagement.Verify(
            management => management.ReplaceUserRoleAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenTargetHasCaregiverProfileAndNewRoleIsNotCaregiver_IsRejected()
    {
        var actorId = Guid.NewGuid();
        var target = User(DomainUserRole.Caregiver);
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(DomainUserRole.Admin, actorId));
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        unitOfWork.Caregivers.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<Caregiver, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService(unitOfWork, actorId);

        var act = async () => await service.UpdateRoleAsync(
            target.Id,
            new UpdateUserRoleRequest { Role = ContractUserRole.Coordinator });

        await act.Should().ThrowAsync<ResourceConflictException>()
            .Where(exception => exception.Code == "admin.profile_role_coupled");
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenIdentityRoleSyncFails_ThrowsConflictSoTransactionRollsBack()
    {
        var actorId = Guid.NewGuid();
        var target = User(DomainUserRole.Coordinator);
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(DomainUserRole.Admin, actorId));
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        unitOfWork.Users.Setup(repository => repository.UpdateAsync(target, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unitOfWork.Caregivers.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<Caregiver, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        unitOfWork.Clients.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<DomainClient, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var roleManagement = new Mock<IIdentityRoleManagementService>();
        roleManagement.Setup(management => management.ReplaceUserRoleAsync(target.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var auditLogger = new Mock<IPhiAuditLogger>();
        var service = CreateService(unitOfWork, actorId, roleManagement, auditLogger);

        var act = async () => await service.UpdateRoleAsync(
            target.Id,
            new UpdateUserRoleRequest { Role = ContractUserRole.Clinician });

        await act.Should().ThrowAsync<ResourceConflictException>()
            .Where(exception => exception.Code == "identity.role_sync_failed");
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.Action == AuditAction.RoleChanged),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateStaffUserAsync_WhenProvisioningFails_ThrowsGenericValidationWithoutProvisionedAudit()
    {
        var actorId = Guid.NewGuid();
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(DomainUserRole.Admin, actorId));
        unitOfWork.Users.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        unitOfWork.Users.Setup(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User user, CancellationToken _) => user);
        var identityProvisioning = new Mock<IIdentityProvisioningService>();
        identityProvisioning.Setup(provisioning => provisioning.ProvisionUserAsync(It.IsAny<IdentityProvisioningRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityProvisioningResult.Failed("identity.duplicate"));
        var auditLogger = new Mock<IPhiAuditLogger>();
        var service = new AdminUserManagementService(
            unitOfWork,
            new TestCurrentUserContext(actorId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Admin }),
            identityProvisioning.Object,
            Mock.Of<IIdentityRoleManagementService>(),
            auditLogger.Object);

        var act = async () => await service.CreateStaffUserAsync(ValidStaffRequest());

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Message.Should().NotContainAny("staff@example.test", "ValidPass1");
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.Action == AuditAction.StaffProvisioned),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateStaffUserAsync_WhenEmailAlreadyExists_ThrowsGenericValidationWithoutEchoingEmail()
    {
        var actorId = Guid.NewGuid();
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(DomainUserRole.Admin, actorId));
        unitOfWork.Users.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService(unitOfWork, actorId);

        var act = async () => await service.CreateStaffUserAsync(ValidStaffRequest());

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Message.Should().NotContain("staff@example.test");
    }

    [Fact]
    public async Task CreateStaffUserAsync_WhenValid_AuditsStaffProvisionedWithRoleEnumOnly()
    {
        var actorId = Guid.NewGuid();
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(DomainUserRole.Admin, actorId));
        unitOfWork.Users.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        unitOfWork.Users.Setup(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User user, CancellationToken _) => user);
        unitOfWork.Users.Setup(repository => repository.CountAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        unitOfWork.Caregivers.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<Caregiver, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        unitOfWork.Clients.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<DomainClient, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var identityProvisioning = new Mock<IIdentityProvisioningService>();
        identityProvisioning.Setup(provisioning => provisioning.ProvisionUserAsync(It.IsAny<IdentityProvisioningRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityProvisioningResult.Success());
        var auditLogger = new Mock<IPhiAuditLogger>();
        auditLogger.Setup(logger => logger.LogAsync(It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = new AdminUserManagementService(
            unitOfWork,
            new TestCurrentUserContext(actorId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Admin }),
            identityProvisioning.Object,
            Mock.Of<IIdentityRoleManagementService>(),
            auditLogger.Object);

        var result = await service.CreateStaffUserAsync(ValidStaffRequest());

        result.Role.Should().Be(ContractUserRole.Coordinator);
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry =>
                entry.Action == AuditAction.StaffProvisioned &&
                entry.EntityType == ProtectedResourceType.UserAccount &&
                entry.Attributes != null &&
                entry.Attributes.Count == 1 &&
                entry.Attributes["Role"] == DomainUserRole.Coordinator.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateStatusAsync_WhenStatusChanges_UpdatesUserAndAuditsWithoutIdentifyingValues(bool activate)
    {
        var actorId = Guid.NewGuid();
        var target = User(DomainUserRole.Coordinator);
        target.IsActive = !activate;
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(DomainUserRole.Admin, actorId));
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        unitOfWork.Users.Setup(repository => repository.UpdateAsync(target, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unitOfWork.Caregivers.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<Caregiver, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        unitOfWork.Clients.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<DomainClient, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var auditLogger = new Mock<IPhiAuditLogger>();
        auditLogger.Setup(logger => logger.LogAsync(It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(unitOfWork, actorId, auditLogger: auditLogger);

        var result = await service.UpdateStatusAsync(target.Id, new UpdateUserStatusRequest { IsActive = activate });

        result.IsActive.Should().Be(activate);
        target.IsActive.Should().Be(activate);
        var expectedAction = activate ? AuditAction.AccountActivated : AuditAction.AccountDeactivated;
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry =>
                entry.Action == expectedAction &&
                entry.EntityType == ProtectedResourceType.UserAccount &&
                entry.EntityId == target.Id &&
                entry.Attributes == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static void SetupBatchedProfileLookups(MockUnitOfWork unitOfWork, int activeAdminCount)
    {
        unitOfWork.Caregivers.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Caregiver, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Caregiver>());
        unitOfWork.Clients.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<DomainClient, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DomainClient>());
        unitOfWork.Users.Setup(repository => repository.CountAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeAdminCount);
    }

    private static CreateStaffUserRequest ValidStaffRequest() => new()
    {
        FirstName = "Test",
        LastName = "Staff",
        Email = "staff@example.test",
        PhoneNumber = "555-0100",
        TemporaryPassword = "ValidPass1",
        Role = ContractUserRole.Coordinator,
    };

    private static AdminUserManagementService CreateService(
        MockUnitOfWork unitOfWork,
        Guid actorId,
        Mock<IIdentityRoleManagementService>? roleManagement = null,
        Mock<IPhiAuditLogger>? auditLogger = null)
    {
        return new AdminUserManagementService(
            unitOfWork,
            new TestCurrentUserContext(actorId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Admin }),
            Mock.Of<IIdentityProvisioningService>(),
            roleManagement?.Object ?? Mock.Of<IIdentityRoleManagementService>(),
            auditLogger?.Object ?? Mock.Of<IPhiAuditLogger>());
    }

    private static MockUnitOfWork CreateUnitOfWork() => new();

    private static User User(
        DomainUserRole role,
        Guid? id = null,
        string firstName = "Test",
        string lastName = "User",
        string? email = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = email ?? $"{Guid.NewGuid():N}@example.test",
            PhoneNumber = "555-0100",
            Role = role,
            IsActive = true,
        };

    private sealed record TestCurrentUserContext(
        Guid? UserId,
        IReadOnlySet<string> Roles) : ICurrentUserContext
    {
        public string? UserName => "admin@example.test";

        public bool IsAuthenticated => UserId.HasValue;

        public string? CorrelationId => "test-correlation";
    }

    private sealed class MockUnitOfWork : IUnitOfWork
    {
        public Mock<IUnitOfWork> Mock { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<User>> Users { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<Caregiver>> Caregivers { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<DomainClient>> Clients { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<CaregiverCertification>> CaregiverCertifications { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<ClientAccessGrant>> ClientAccessGrants { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<CarePlan>> CarePlans { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<Shift>> Shifts { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<VisitNote>> VisitNotes { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<VisitPhoto>> VisitPhotos { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<Invoice>> Invoices { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<InvoiceLineItem>> InvoiceLineItems { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<Payment>> Payments { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<DischargeDocument>> DischargeDocuments { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<TransitionPlan>> TransitionPlans { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<TransitionInstruction>> TransitionInstructions { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<TransitionReminder>> TransitionReminders { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<TransitionCheckIn>> TransitionCheckIns { get; } = new(MockBehavior.Strict);

        public Mock<IRepository<TransitionEscalation>> TransitionEscalations { get; } = new(MockBehavior.Strict);

        public MockUnitOfWork()
        {
            Mock.Setup(work => work.ExecuteInTransactionAsync(
                    It.IsAny<IsolationLevel>(),
                    It.IsAny<Func<CancellationToken, Task>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Mock.Setup(work => work.ExecuteInTransactionAsync(
                    It.IsAny<IsolationLevel>(),
                    It.IsAny<Func<CancellationToken, Task<UserAccountDto>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<UserAccountDto>(null!));
            Mock.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        IRepository<User> IUnitOfWork.Users => Users.Object;

        IRepository<Caregiver> IUnitOfWork.Caregivers => Caregivers.Object;

        IRepository<CaregiverCertification> IUnitOfWork.CaregiverCertifications => CaregiverCertifications.Object;

        IRepository<DomainClient> IUnitOfWork.Clients => Clients.Object;

        IRepository<ClientAccessGrant> IUnitOfWork.ClientAccessGrants => ClientAccessGrants.Object;

        IRepository<CarePlan> IUnitOfWork.CarePlans => CarePlans.Object;

        IRepository<Shift> IUnitOfWork.Shifts => Shifts.Object;

        IRepository<VisitNote> IUnitOfWork.VisitNotes => VisitNotes.Object;

        IRepository<VisitPhoto> IUnitOfWork.VisitPhotos => VisitPhotos.Object;

        IRepository<Invoice> IUnitOfWork.Invoices => Invoices.Object;

        IRepository<InvoiceLineItem> IUnitOfWork.InvoiceLineItems => InvoiceLineItems.Object;

        IRepository<Payment> IUnitOfWork.Payments => Payments.Object;

        IRepository<DischargeDocument> IUnitOfWork.DischargeDocuments => DischargeDocuments.Object;

        IRepository<TransitionPlan> IUnitOfWork.TransitionPlans => TransitionPlans.Object;

        IRepository<TransitionInstruction> IUnitOfWork.TransitionInstructions => TransitionInstructions.Object;

        IRepository<TransitionReminder> IUnitOfWork.TransitionReminders => TransitionReminders.Object;

        IRepository<TransitionCheckIn> IUnitOfWork.TransitionCheckIns => TransitionCheckIns.Object;

        IRepository<TransitionEscalation> IUnitOfWork.TransitionEscalations => TransitionEscalations.Object;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Mock.Object.SaveChangesAsync(cancellationToken);

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) =>
            ExecuteInTransactionAsync(IsolationLevel.ReadCommitted, operation, cancellationToken);

        public async Task ExecuteInTransactionAsync(IsolationLevel isolationLevel, Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            await Mock.Object.ExecuteInTransactionAsync(isolationLevel, operation, cancellationToken);
            await operation(cancellationToken);
        }

        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
            ExecuteInTransactionAsync(IsolationLevel.ReadCommitted, operation, cancellationToken);

        public async Task<TResult> ExecuteInTransactionAsync<TResult>(IsolationLevel isolationLevel, Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
        {
            await Mock.Object.ExecuteInTransactionAsync(isolationLevel, operation, cancellationToken);
            return await operation(cancellationToken);
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
