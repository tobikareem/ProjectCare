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
    public async Task GetUsersAsync_WhenSearchIsProvided_FiltersByDisplayNameAndEmail()
    {
        var actorId = Guid.NewGuid();
        var matchingByName = User(DomainUserRole.Admin, firstName: "Nina", lastName: "Patel", email: "nina@example.test");
        var matchingByEmail = User(DomainUserRole.Coordinator, firstName: "Alex", lastName: "Rivera", email: "coordinator@example.test");
        var nonMatching = User(DomainUserRole.Clinician, firstName: "Rae", lastName: "Kim", email: "clinician@example.test");
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(DomainUserRole.Admin, actorId));
        unitOfWork.Users.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([matchingByName, matchingByEmail, nonMatching]);
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
        var service = CreateService(unitOfWork, actorId);

        var result = await service.GetUsersAsync(new PagedRequest { PageNumber = 1, PageSize = 10 }, search: "nina");

        result.Items.Should().ContainSingle(item => item.Id == matchingByName.Id);
        result.Items.Should().NotContain(item => item.Id == matchingByEmail.Id || item.Id == nonMatching.Id);
    }

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
