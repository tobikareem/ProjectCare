using System.Linq.Expressions;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Scheduling.Services;
using CarePath.Contracts.Scheduling;
using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentAssertions;
using FluentValidation;
using Moq;
using ContractServiceType = CarePath.Contracts.Enumerations.ServiceType;

namespace CarePath.Application.Tests.Operations;

public sealed class Sprint4SchedulingServiceTests
{
    private static readonly DateTime WindowStart = new(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = new(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateShiftAsync_WhenExistingShiftTouchesRequestedStart_AllowsAssignment()
    {
        // Arrange
        var context = CreateContext(existingShifts: [ExistingShift(WindowStart.AddHours(-4), WindowStart, ShiftStatus.Scheduled)]);

        // Act
        var result = await context.Service.CreateShiftAsync(CreateRequest(context.Client.Id, context.Caregiver.Id));

        // Assert
        result.ClientId.Should().Be(context.Client.Id);
        context.UnitOfWork.Shifts.Verify(repository => repository.AddAsync(It.IsAny<Shift>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateShiftAsync_WhenRequestedEndTouchesExistingShiftStart_AllowsAssignment()
    {
        // Arrange
        var context = CreateContext(existingShifts: [ExistingShift(WindowEnd, WindowEnd.AddHours(4), ShiftStatus.Scheduled)]);

        // Act
        var result = await context.Service.CreateShiftAsync(CreateRequest(context.Client.Id, context.Caregiver.Id));

        // Assert
        result.ClientId.Should().Be(context.Client.Id);
        context.UnitOfWork.Shifts.Verify(repository => repository.AddAsync(It.IsAny<Shift>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task CreateShiftAsync_WhenExistingShiftContainsRequestedWindow_ThrowsDoubleBookedCode()
    {
        // Arrange
        var context = CreateContext(existingShifts: [ExistingShift(WindowStart.AddHours(-1), WindowEnd.AddHours(1), ShiftStatus.Scheduled)]);

        // Act
        var act = async () => await context.Service.CreateShiftAsync(CreateRequest(context.Client.Id, context.Caregiver.Id));

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle(error => error.ErrorCode == "shift.double_booked");
        context.UnitOfWork.Shifts.Verify(repository => repository.AddAsync(It.IsAny<Shift>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateShiftAsync_WhenRequestedWindowSpansExistingShift_ThrowsDoubleBookedCode()
    {
        // Arrange
        var context = CreateContext(existingShifts: [ExistingShift(WindowStart.AddHours(1), WindowEnd.AddHours(-1), ShiftStatus.InProgress)]);

        // Act
        var act = async () => await context.Service.CreateShiftAsync(CreateRequest(context.Client.Id, context.Caregiver.Id));

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle(error => error.ErrorCode == "shift.double_booked");
    }

    [Fact]
    public async Task CreateShiftAsync_WhenOnlyCancelledAndCompletedShiftsOverlap_AllowsAssignment()
    {
        // Arrange
        var context = CreateContext(existingShifts:
        [
            ExistingShift(WindowStart.AddHours(-1), WindowEnd.AddHours(1), ShiftStatus.Cancelled),
            ExistingShift(WindowStart.AddHours(-1), WindowEnd.AddHours(1), ShiftStatus.Completed),
        ]);

        // Act
        var result = await context.Service.CreateShiftAsync(CreateRequest(context.Client.Id, context.Caregiver.Id));

        // Assert
        result.Id.Should().NotBeEmpty();
        context.UnitOfWork.Shifts.Verify(repository => repository.AddAsync(It.IsAny<Shift>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateShiftAsync_WhenCaregiverHasNoMatchingCertification_ThrowsCertificationExpiredCode()
    {
        // Arrange
        var context = CreateContext(certifications: Array.Empty<CaregiverCertification>());

        // Act
        var act = async () => await context.Service.CreateShiftAsync(CreateRequest(context.Client.Id, context.Caregiver.Id));

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle(error => error.ErrorCode == "caregiver.certification_expired");
    }
    [Fact]
    public async Task CreateShiftAsync_WhenCertificationExpiredBeforeShiftDate_ThrowsCertificationExpiredCode()
    {
        // Arrange
        var context = CreateContext(certifications: [Certification(WindowStart.Date.AddDays(-1))]);

        // Act
        var act = async () => await context.Service.CreateShiftAsync(CreateRequest(context.Client.Id, context.Caregiver.Id));

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle(error => error.ErrorCode == "caregiver.certification_expired");
        context.UnitOfWork.Shifts.Verify(repository => repository.ExistsAsync(It.IsAny<Expression<Func<Shift, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateShiftAsync_WhenExpiredCredentialHasValidReplacement_AllowsAssignment()
    {
        // Arrange
        var context = CreateContext(certifications:
        [
            Certification(WindowStart.Date.AddDays(-10)),
            Certification(WindowStart.Date.AddDays(10)),
        ]);

        // Act
        var result = await context.Service.CreateShiftAsync(CreateRequest(context.Client.Id, context.Caregiver.Id));

        // Assert
        result.CaregiverId.Should().Be(context.Caregiver.Id);
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.Action == AuditAction.Read && entry.EntityType == ProtectedResourceType.CaregiverCertification),
            It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }
    [Fact]
    public async Task CreateShiftAsync_WhenCertificationExpiresOnShiftDate_AllowsAssignment()
    {
        // Arrange
        var context = CreateContext(certifications: [Certification(WindowStart.Date)]);

        // Act
        var result = await context.Service.CreateShiftAsync(CreateRequest(context.Client.Id, context.Caregiver.Id));

        // Assert
        result.CaregiverId.Should().Be(context.Caregiver.Id);
        context.UnitOfWork.Shifts.Verify(repository => repository.AddAsync(It.IsAny<Shift>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateShiftAsync_WhenUpdatedWindowOverlapsExistingShift_ThrowsDoubleBookedCode()
    {
        // Arrange
        var context = CreateContext(existingShifts: [ExistingShift(WindowStart.AddHours(1), WindowEnd.AddHours(1), ShiftStatus.Scheduled)]);
        var shift = ExistingShift(WindowStart.AddDays(1), WindowEnd.AddDays(1), ShiftStatus.Scheduled);
        shift.ClientId = context.Client.Id;
        shift.CaregiverId = context.Caregiver.Id;
        context.UnitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        var request = new UpdateShiftRequest
        {
            CaregiverId = context.Caregiver.Id,
            ScheduledStartUtc = WindowStart,
            ScheduledEndUtc = WindowEnd,
            BillRate = 40m,
            PayRate = 24m,
            BreakMinutes = 0,
            ServiceType = ContractServiceType.InHomeCare,
        };

        // Act
        var act = async () => await context.Service.UpdateShiftAsync(shift.Id, request);

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle(error => error.ErrorCode == "shift.double_booked");
    }

    [Fact]
    public async Task GetShiftsAsync_WhenCaregiverScoped_UsesFilteredPagedRepository()
    {
        // Arrange
        var context = CreateContext();
        var shift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.Scheduled);
        shift.ClientId = context.Client.Id;
        shift.CaregiverId = context.Caregiver.Id;
        var service = new ShiftOperationsService(
            context.UnitOfWork,
            new TestCurrentUserContext(context.Caregiver.UserId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Caregiver }),
            Mock.Of<IClientAccessEvaluator>(),
            context.AuditLogger.Object);
        context.UnitOfWork.Caregivers.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { context.Caregiver });
        context.UnitOfWork.Shifts.Setup(repository => repository.GetPagedAsync(It.IsAny<Expression<Func<Shift, bool>>>(), 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { shift }, 1));

        // Act
        var result = await service.GetShiftsAsync(new CarePath.Contracts.Common.PagedRequest { PageNumber = 1, PageSize = 10 });

        // Assert
        result.Items.Should().ContainSingle(item => item.Id == shift.Id);
        context.UnitOfWork.Shifts.Verify(repository => repository.GetPagedAsync(It.IsAny<Expression<Func<Shift, bool>>>(), 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetShiftsAsync_WhenClientGrantScopesList_AuditsGrantRead()
    {
        // Arrange
        var context = CreateContext();
        var grantedUser = User(UserRole.Client);
        var grantedClient = new Client
        {
            Id = Guid.NewGuid(),
            UserId = grantedUser.Id,
            DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            User = grantedUser,
        };
        var grant = new ClientAccessGrant
        {
            Id = Guid.NewGuid(),
            ClientId = grantedClient.Id,
            GranteeUserId = context.Client.UserId,
            AccessScope = AccessScope.Full,
        };
        var shift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.Scheduled);
        shift.ClientId = grantedClient.Id;
        shift.CaregiverId = context.Caregiver.Id;
        context.UnitOfWork.Clients.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Client, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { context.Client });
        context.UnitOfWork.ClientAccessGrants.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<ClientAccessGrant, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { grant });
        context.UnitOfWork.Shifts.Setup(repository => repository.GetPagedAsync(It.IsAny<Expression<Func<Shift, bool>>>(), 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { shift }, 1));
        context.UnitOfWork.Clients.Setup(repository => repository.GetByIdAsync(grantedClient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grantedClient);
        context.UnitOfWork.Users.Setup(repository => repository.GetByIdAsync(grantedClient.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(grantedUser);
        var service = new ShiftOperationsService(
            context.UnitOfWork,
            new TestCurrentUserContext(context.Client.UserId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Client }),
            Mock.Of<IClientAccessEvaluator>(),
            context.AuditLogger.Object);

        // Act
        var result = await service.GetShiftsAsync(new CarePath.Contracts.Common.PagedRequest { PageNumber = 1, PageSize = 10 });

        // Assert
        result.Items.Should().ContainSingle(item => item.Id == shift.Id);
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.Action == AuditAction.Read && entry.EntityType == ProtectedResourceType.ClientAccessGrant && entry.EntityId == grant.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task CheckInAsync_WhenShiftLifecycleIsInvalid_ThrowsStableValidationCode()
    {
        // Arrange
        var context = CreateContext();
        var shift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.Cancelled);
        shift.ClientId = context.Client.Id;
        shift.CaregiverId = context.Caregiver.Id;
        context.UnitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        context.UnitOfWork.Caregivers.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { context.Caregiver });
        var service = new ShiftOperationsService(
            context.UnitOfWork,
            new TestCurrentUserContext(context.Caregiver.UserId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Caregiver }),
            Mock.Of<IClientAccessEvaluator>(),
            context.AuditLogger.Object);

        // Act
        var act = async () => await service.CheckInAsync(new CheckInRequest
        {
            ShiftId = shift.Id,
            Latitude = 39.0,
            Longitude = -76.0,
            TimestampUtc = WindowStart,
        });

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle(error => error.ErrorCode == "shift.invalid_lifecycle");
    }
    [Fact]
    public async Task CheckInAsync_WhenCallerIsNotAssignedCaregiver_AuditsDeniedAndThrowsWithoutDisclosureException()
    {
        // Arrange
        var context = CreateContext();
        var assignedShift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.Scheduled);
        assignedShift.ClientId = context.Client.Id;
        assignedShift.CaregiverId = context.Caregiver.Id;
        var otherCaregiver = new Caregiver { Id = Guid.NewGuid(), UserId = context.CurrentUserId };
        context.UnitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(assignedShift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assignedShift);
        context.UnitOfWork.Caregivers.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { otherCaregiver });
        var request = new CheckInRequest
        {
            ShiftId = assignedShift.Id,
            Latitude = 39.0,
            Longitude = -76.0,
            TimestampUtc = WindowStart,
        };

        // Act
        var act = async () => await context.Service.CheckInAsync(request);

        // Assert
        await act.Should().ThrowAsync<ResourceAccessDeniedException>()
            .Where(exception => exception.IsPhiResource && exception.ReasonCode == "NotAssigned");
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.Action == AuditAction.AccessDenied && entry.EntityType == ProtectedResourceType.Shift && entry.EntityId == assignedShift.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static TestContext CreateContext(
        IReadOnlyList<Shift>? existingShifts = null,
        IReadOnlyList<CaregiverCertification>? certifications = null)
    {
        var unitOfWork = new MockUnitOfWork();
        var currentUserId = Guid.NewGuid();
        var clientUser = User(UserRole.Client);
        var caregiverUser = User(UserRole.Caregiver);
        var client = new Client
        {
            Id = Guid.NewGuid(),
            UserId = clientUser.Id,
            User = clientUser,
            DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var caregiver = new Caregiver
        {
            Id = Guid.NewGuid(),
            UserId = caregiverUser.Id,
            User = caregiverUser,
        };

        foreach (var existingShift in existingShifts ?? Array.Empty<Shift>())
        {
            existingShift.CaregiverId = caregiver.Id;
            existingShift.ClientId = client.Id;
        }

        unitOfWork.Clients.Setup(repository => repository.GetByIdAsync(client.Id, It.IsAny<CancellationToken>())).ReturnsAsync(client);
        unitOfWork.Caregivers.Setup(repository => repository.GetByIdAsync(caregiver.Id, It.IsAny<CancellationToken>())).ReturnsAsync(caregiver);
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(client.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(clientUser);
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(caregiver.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(caregiverUser);
        var certificationRows = (certifications ?? new[] { Certification(WindowStart.Date.AddDays(1)) }).ToArray();
        foreach (var certification in certificationRows)
        {
            certification.CaregiverId = caregiver.Id;
        }

        unitOfWork.CaregiverCertifications.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<CaregiverCertification, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(certificationRows);
        unitOfWork.Shifts.Setup(repository => repository.ExistsAsync(It.IsAny<Expression<Func<Shift, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Shift, bool>> predicate, CancellationToken _) => (existingShifts ?? Array.Empty<Shift>()).Any(predicate.Compile()));
        unitOfWork.Shifts.Setup(repository => repository.AddAsync(It.IsAny<Shift>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shift shift, CancellationToken _) =>
            {
                return shift;
            });
        unitOfWork.Shifts.Setup(repository => repository.UpdateAsync(It.IsAny<Shift>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var auditLogger = new Mock<IPhiAuditLogger>();
        auditLogger.Setup(logger => logger.LogAsync(It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new ShiftOperationsService(
            unitOfWork,
            new TestCurrentUserContext(currentUserId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Admin }),
            Mock.Of<IClientAccessEvaluator>(),
            auditLogger.Object);

        return new TestContext(unitOfWork, service, auditLogger, currentUserId, client, caregiver);
    }

    private static CreateShiftRequest CreateRequest(Guid clientId, Guid caregiverId) => new()
    {
        ClientId = clientId,
        CaregiverId = caregiverId,
        ScheduledStartUtc = WindowStart,
        ScheduledEndUtc = WindowEnd,
        BillRate = 40m,
        PayRate = 24m,
        BreakMinutes = 0,
        ServiceType = ContractServiceType.InHomeCare,
    };

    private static Shift ExistingShift(DateTime start, DateTime end, ShiftStatus status) => new()
    {
        Id = Guid.NewGuid(),
        CaregiverId = Guid.Empty,
        ScheduledStartTime = start,
        ScheduledEndTime = end,
        Status = status,
        ServiceType = ServiceType.InHomeCare,
    };

    private static CaregiverCertification Certification(DateTime expirationDate) => new()
    {
        Id = Guid.NewGuid(),
        CaregiverId = Guid.NewGuid(),
        Type = CertificationType.CNA,
        IssueDate = expirationDate.AddYears(-1),
        ExpirationDate = expirationDate,
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

    private sealed record TestContext(
        MockUnitOfWork UnitOfWork,
        ShiftOperationsService Service,
        Mock<IPhiAuditLogger> AuditLogger,
        Guid CurrentUserId,
        Client Client,
        Caregiver Caregiver);

    private sealed record TestCurrentUserContext(Guid? UserId, IReadOnlySet<string> Roles) : ICurrentUserContext
    {
        public string? UserName => "test-user@example.test";

        public bool IsAuthenticated => UserId.HasValue;

        public string? CorrelationId => "test-correlation";
    }

    private sealed class MockUnitOfWork : IUnitOfWork
    {
        public Mock<IUnitOfWork> Mock { get; } = new(MockBehavior.Strict);

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
            Mock.Setup(work => work.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Mock.Setup(work => work.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Mock.Setup(work => work.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Mock.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        IRepository<User> IUnitOfWork.Users => Users.Object;

        IRepository<Caregiver> IUnitOfWork.Caregivers => Caregivers.Object;

        IRepository<CaregiverCertification> IUnitOfWork.CaregiverCertifications => CaregiverCertifications.Object;

        IRepository<Client> IUnitOfWork.Clients => Clients.Object;

        IRepository<ClientAccessGrant> IUnitOfWork.ClientAccessGrants => ClientAccessGrants.Object;

        IRepository<CarePlan> IUnitOfWork.CarePlans => CarePlans.Object;

        IRepository<Shift> IUnitOfWork.Shifts => Shifts.Object;

        IRepository<VisitNote> IUnitOfWork.VisitNotes => VisitNotes.Object;

        IRepository<VisitPhoto> IUnitOfWork.VisitPhotos => VisitPhotos.Object;

        IRepository<Invoice> IUnitOfWork.Invoices => Invoices.Object;

        IRepository<InvoiceLineItem> IUnitOfWork.InvoiceLineItems => InvoiceLineItems.Object;

        IRepository<Payment> IUnitOfWork.Payments => Payments.Object;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Mock.Object.SaveChangesAsync(cancellationToken);

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Mock.Object.BeginTransactionAsync(cancellationToken);

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Mock.Object.CommitTransactionAsync(cancellationToken);

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Mock.Object.RollbackTransactionAsync(cancellationToken);

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}






