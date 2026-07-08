using System.Linq.Expressions;
using System.Data;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Clients.Services;
using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Entities.Transitions;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentAssertions;
using Moq;
using DomainClient = global::CarePath.Domain.Entities.Identity.Client;
using ContractAccessScope = CarePath.Contracts.Enumerations.AccessScope;

namespace CarePath.Application.Tests.Operations;

public sealed class Sprint4GrantServiceTests
{
    [Fact]
    public async Task GetGrantsAsync_WhenGrantIsRevoked_DoesNotReturnRevokedGrant()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var grantee = User(UserRole.Client);
        var activeGrant = Grant(clientId, grantee.Id, revoked: false);
        var revokedGrant = Grant(clientId, grantee.Id, revoked: true);
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Clients.Setup(repository => repository.GetByIdAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DomainClient { Id = clientId, UserId = Guid.NewGuid(), DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
        unitOfWork.ClientAccessGrants.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<ClientAccessGrant, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<ClientAccessGrant, bool>> predicate, CancellationToken _) => new[] { activeGrant, revokedGrant }.Where(predicate.Compile()).ToArray());
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(grantee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grantee);
        var auditLogger = new Mock<IPhiAuditLogger>();
        auditLogger.Setup(logger => logger.LogAsync(It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new ClientAccessGrantService(
            unitOfWork,
            new TestCurrentUserContext(Guid.NewGuid(), new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Admin }),
            auditLogger.Object);

        // Act
        var result = await service.GetGrantsAsync(clientId);

        // Assert
        result.Should().ContainSingle(grant => grant.Id == activeGrant.Id);
        result.Should().NotContain(grant => grant.Id == revokedGrant.Id);
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.ClientAccessGrant && entry.Action == AuditAction.Read),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static MockUnitOfWork CreateUnitOfWork() => new();

    private static ClientAccessGrant Grant(Guid clientId, Guid granteeUserId, bool revoked) => new()
    {
        Id = Guid.NewGuid(),
        ClientId = clientId,
        GranteeUserId = granteeUserId,
        GrantedByUserId = Guid.NewGuid(),
        AccessScope = AccessScope.Full,
        GrantedAtUtc = DateTime.UtcNow.AddDays(-1),
        RevokedAtUtc = revoked ? DateTime.UtcNow : null,
    };

    private static User User(UserRole role) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Test",
        LastName = "User",
        Email = $"{Guid.NewGuid():N}@example.test",
        PhoneNumber = "555-0100",
        Role = role,
    };

    private sealed record TestCurrentUserContext(Guid? UserId, IReadOnlySet<string> Roles) : ICurrentUserContext
    {
        public string? UserName => "test-user@example.test";

        public bool IsAuthenticated => UserId.HasValue;

        public string? CorrelationId => "test-correlation";
    }

    private sealed class MockUnitOfWork : IUnitOfWork
    {
        public Mock<IRepository<User>> Users { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<Caregiver>> Caregivers { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<CaregiverCertification>> CaregiverCertifications { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<DomainClient>> Clients { get; } = new(MockBehavior.Strict);
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
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) =>
            operation(cancellationToken);

        public Task ExecuteInTransactionAsync(IsolationLevel isolationLevel, Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
        public Task<TResult> ExecuteInTransactionAsync<TResult>(IsolationLevel isolationLevel, Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
