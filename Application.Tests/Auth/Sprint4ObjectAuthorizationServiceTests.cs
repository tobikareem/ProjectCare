using System.Linq.Expressions;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Auth;
using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentAssertions;
using Moq;

namespace CarePath.Application.Tests.Auth;

public sealed class Sprint4ObjectAuthorizationServiceTests
{
    [Fact]
    public async Task AuthorizeAsync_WhenCaregiverOwnsShift_AuthorizesShiftRead()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var unitOfWork = new MockUnitOfWork();
        unitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(shiftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Shift { Id = shiftId, ClientId = Guid.NewGuid(), CaregiverId = caregiverId });
        unitOfWork.Caregivers.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Caregiver { Id = caregiverId, UserId = userId } });
        var service = new Sprint4ObjectAuthorizationService(unitOfWork.Object, Mock.Of<IClientAccessEvaluator>());

        // Act
        var result = await service.AuthorizeAsync(new ObjectAccessRequest(
            new TestCurrentUserContext(userId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Caregiver }),
            ProtectedResourceType.Shift,
            shiftId,
            ObjectAccessAction.Read,
            "test-correlation"));

        // Assert
        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_WhenClientHasNoGrant_DeniesClientRead()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var unitOfWork = new MockUnitOfWork();
        unitOfWork.Clients.Setup(repository => repository.ExistsAsync(It.IsAny<Expression<Func<Client, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var evaluator = new Mock<IClientAccessEvaluator>(MockBehavior.Strict);
        evaluator.Setup(service => service.EvaluateAsync(userId, clientId, AccessScope.Full, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientAccessEvaluationResult.Denied(ClientAccessEvaluationResult.NoGrant));
        var service = new Sprint4ObjectAuthorizationService(unitOfWork.Object, evaluator.Object);

        // Act
        var result = await service.AuthorizeAsync(new ObjectAccessRequest(
            new TestCurrentUserContext(userId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Client }),
            ProtectedResourceType.Client,
            clientId,
            ObjectAccessAction.Read,
            "test-correlation"));

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.DenialCode.Should().Be("NoGrant");
    }

    private sealed record TestCurrentUserContext(Guid? UserId, IReadOnlySet<string> Roles) : ICurrentUserContext
    {
        public string? UserName => "test-user@example.test";
        public bool IsAuthenticated => UserId.HasValue;
        public string? CorrelationId => "test-correlation";
    }

    private sealed class MockUnitOfWork
    {
        private readonly Mock<IUnitOfWork> mock = new(MockBehavior.Strict);
        public Mock<IRepository<User>> Users { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<Caregiver>> Caregivers { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<CaregiverCertification>> CaregiverCertifications { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<Client>> Clients { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<ClientAccessGrant>> ClientAccessGrants { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<CarePlan>> CarePlans { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<Shift>> Shifts { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<VisitNote>> VisitNotes { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<VisitPhoto>> VisitPhotos { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<Invoice>> Invoices { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<InvoiceLineItem>> InvoiceLineItems { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<Payment>> Payments { get; } = new(MockBehavior.Strict);

        public MockUnitOfWork()
        {
            mock.SetupGet(work => work.Users).Returns(Users.Object);
            mock.SetupGet(work => work.Caregivers).Returns(Caregivers.Object);
            mock.SetupGet(work => work.CaregiverCertifications).Returns(CaregiverCertifications.Object);
            mock.SetupGet(work => work.Clients).Returns(Clients.Object);
            mock.SetupGet(work => work.ClientAccessGrants).Returns(ClientAccessGrants.Object);
            mock.SetupGet(work => work.CarePlans).Returns(CarePlans.Object);
            mock.SetupGet(work => work.Shifts).Returns(Shifts.Object);
            mock.SetupGet(work => work.VisitNotes).Returns(VisitNotes.Object);
            mock.SetupGet(work => work.VisitPhotos).Returns(VisitPhotos.Object);
            mock.SetupGet(work => work.Invoices).Returns(Invoices.Object);
            mock.SetupGet(work => work.InvoiceLineItems).Returns(InvoiceLineItems.Object);
            mock.SetupGet(work => work.Payments).Returns(Payments.Object);
        }

        public IUnitOfWork Object => mock.Object;
    }
}
