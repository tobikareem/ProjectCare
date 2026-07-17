using System.Linq.Expressions;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Scheduling.Queries;
using CarePath.Application.Scheduling.Services;
using CarePath.Contracts.Common;
using CarePath.Contracts.Enumerations;
using CarePath.Contracts.Scheduling;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Interfaces.Repositories;
using FluentAssertions;
using FluentValidation;
using Moq;
using DomainClient = CarePath.Domain.Entities.Identity.Client;

namespace CarePath.Application.Tests.Operations;

public sealed class AssignmentHistoryServiceTests
{
    [Fact]
    public async Task GetCaregiversForClientAsync_Admin_ReturnsPageAndAuditsParentAndRows()
    {
        var clientId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var query = new Mock<IAssignmentHistoryQuery>();
        query.Setup(item => item.GetCaregiversForClientAsync(clientId, It.IsAny<AssignmentHistorySearchRequest>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CaregiverAssignmentSummaryDto> { Items = [new(caregiverId, "Test Caregiver", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), 1, AssignmentRelationshipStatus.Current)], PageNumber = 1, PageSize = 20, TotalCount = 1 });
        var audit = AuditLogger();
        var service = CreateService(query.Object, Roles(ApplicationRoles.Admin), audit: audit);

        var result = await service.GetCaregiversForClientAsync(clientId, new AssignmentHistorySearchRequest());

        result.TotalCount.Should().Be(1);
        audit.Verify(logger => logger.LogAsync(It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.Client && entry.EntityId == clientId), It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(logger => logger.LogAsync(It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.Caregiver && entry.EntityId == caregiverId), It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(logger => logger.LogAsync(It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.AssignmentRelationship), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetClientsForCaregiverAsync_CaregiverRole_DeniesStaffSurface()
    {
        var query = new Mock<IAssignmentHistoryQuery>(MockBehavior.Strict);
        var service = CreateService(query.Object, Roles(ApplicationRoles.Caregiver));

        var act = () => service.GetClientsForCaregiverAsync(Guid.NewGuid(), new AssignmentHistorySearchRequest());

        await act.Should().ThrowAsync<ResourceAccessDeniedException>();
    }

    [Fact]
    public async Task GetMyClientsAsync_Caregiver_DerivesProfileAndAbbreviatesClientName()
    {
        var userId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var caregiverRepository = new Mock<IRepository<Caregiver>>();
        caregiverRepository.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Caregiver { Id = caregiverId, UserId = userId }]);
        var query = new Mock<IAssignmentHistoryQuery>();
        query.Setup(item => item.GetClientsForCaregiverAsync(caregiverId, It.IsAny<AssignmentHistorySearchRequest>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ClientAssignmentSummaryDto> { Items = [new(clientId, "Jordan Mitchell", ServiceType.InHomeCare, DateTime.UtcNow.AddDays(-3), DateTime.UtcNow, DateTime.UtcNow, null, 2, AssignmentRelationshipStatus.Previous)], PageNumber = 1, PageSize = 20, TotalCount = 1 });
        var service = CreateService(query.Object, new TestCurrentUser(userId, Roles(ApplicationRoles.Caregiver).Roles), caregiverRepository);

        var result = await service.GetMyClientsAsync(new AssignmentHistorySearchRequest());

        result.Items.Should().ContainSingle().Which.ClientDisplayName.Should().Be("Jordan M.");
        query.Verify(item => item.GetClientsForCaregiverAsync(caregiverId, It.IsAny<AssignmentHistorySearchRequest>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMyClientsAsync_MissingCaregiverProfile_ReturnsPhiSafeNotFound()
    {
        var repository = new Mock<IRepository<Caregiver>>();
        repository.Setup(item => item.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var query = new Mock<IAssignmentHistoryQuery>(MockBehavior.Strict);
        var service = CreateService(query.Object, Roles(ApplicationRoles.Caregiver), repository);

        var act = () => service.GetMyClientsAsync(new AssignmentHistorySearchRequest());

        await act.Should().ThrowAsync<ResourceNotFoundException>().WithMessage("Resource not found.");
    }

    [Fact]
    public async Task SearchAsync_InvalidDateRange_DoesNotQueryPersistence()
    {
        var query = new Mock<IAssignmentHistoryQuery>(MockBehavior.Strict);
        var service = CreateService(query.Object, Roles(ApplicationRoles.Admin));
        var now = DateTime.UtcNow;

        var act = () => service.GetCaregiversForClientAsync(Guid.NewGuid(), new AssignmentHistorySearchRequest { FromUtc = now, ToUtc = now });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GetMyCaregiversAsync_Client_DerivesProfileMinimizesDtoAndAuditsRelationship()
    {
        var userId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var clients = new Mock<IRepository<DomainClient>>();
        clients.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<DomainClient, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DomainClient { Id = clientId, UserId = userId }]);
        var query = new Mock<IAssignmentHistoryQuery>();
        query.Setup(item => item.GetCaregiversForClientAsync(clientId, It.IsAny<AssignmentHistorySearchRequest>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CaregiverAssignmentSummaryDto> { Items = [new(caregiverId, "Amara Williams", DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), 2, AssignmentRelationshipStatus.Current)], PageNumber = 1, PageSize = 20, TotalCount = 1 });
        var audit = AuditLogger();
        var service = CreateService(query.Object, new TestCurrentUser(userId, Roles(ApplicationRoles.Client).Roles), clients: clients, audit: audit);

        var result = await service.GetMyCaregiversAsync(new AssignmentHistorySearchRequest());

        result.Items.Should().ContainSingle().Which.CaregiverDisplayName.Should().Be("Amara W.");
        audit.Verify(logger => logger.LogAsync(It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.AssignmentRelationship), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMyCaregiversAsync_FamilyProxyWithoutOwnedProfile_ReturnsPhiSafeNotFound()
    {
        var clients = new Mock<IRepository<DomainClient>>();
        clients.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<DomainClient, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var service = CreateService(Mock.Of<IAssignmentHistoryQuery>(), Roles(ApplicationRoles.Client), clients: clients);

        var act = () => service.GetMyCaregiversAsync(new AssignmentHistorySearchRequest());

        await act.Should().ThrowAsync<ResourceNotFoundException>().WithMessage("Resource not found.");
    }

    private static AssignmentHistoryService CreateService(IAssignmentHistoryQuery query, TestCurrentUser user, Mock<IRepository<Caregiver>>? caregivers = null, Mock<IRepository<DomainClient>>? clients = null, Mock<IPhiAuditLogger>? audit = null)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(item => item.Caregivers).Returns((caregivers ?? new Mock<IRepository<Caregiver>>()).Object);
        unitOfWork.SetupGet(item => item.Clients).Returns((clients ?? new Mock<IRepository<DomainClient>>()).Object);
        return new AssignmentHistoryService(query, unitOfWork.Object, user, (audit ?? AuditLogger()).Object);
    }

    private static Mock<IPhiAuditLogger> AuditLogger()
    {
        var audit = new Mock<IPhiAuditLogger>();
        audit.Setup(logger => logger.LogAsync(It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return audit;
    }

    private static TestCurrentUser Roles(params string[] roles) => new(Guid.NewGuid(), roles.ToHashSet(StringComparer.Ordinal));

    private sealed record TestCurrentUser(Guid? UserId, IReadOnlySet<string> Roles) : ICurrentUserContext
    {
        public string? UserName => "test@example.test";
        public bool IsAuthenticated => UserId.HasValue;
        public string? CorrelationId => "assignment-test";
    }
}
