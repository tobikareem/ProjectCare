using System.Linq.Expressions;
using CarePath.Application;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Clients.Services;
using CarePath.Application.Clients.Validators;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Identity.Services;
using CarePath.Application.Identity.Validators;
using CarePath.Contracts.Clients;
using CarePath.Contracts.Identity;
using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ContractEmploymentType = CarePath.Contracts.Enumerations.EmploymentType;
using ContractServiceType = CarePath.Contracts.Enumerations.ServiceType;

namespace CarePath.Application.Tests.Operations;

public class Sprint4OperationsServiceTests
{
    [Fact]
    public async Task CreateCaregiverAsync_WhenRequestIsValid_ProvisionsIdentityAndCommitsTransaction()
    {
        // Arrange
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<User>());
        unitOfWork.Users.Setup(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync((User user, CancellationToken _) => user);
        unitOfWork.Caregivers.Setup(repository => repository.AddAsync(It.IsAny<Caregiver>(), It.IsAny<CancellationToken>())).ReturnsAsync((Caregiver caregiver, CancellationToken _) => caregiver);
        var provisioning = new Mock<IIdentityProvisioningService>();
        IdentityProvisioningRequest? provisioningRequest = null;
        provisioning.Setup(service => service.ProvisionUserAsync(It.IsAny<IdentityProvisioningRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IdentityProvisioningRequest, CancellationToken>((request, _) => provisioningRequest = request)
            .ReturnsAsync(IdentityProvisioningResult.Success());
        var service = new CaregiverOperationsService(
            unitOfWork,
            new TestCurrentUserContext(Guid.NewGuid(), new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Admin }),
            provisioning.Object,
            Mock.Of<IPhiAuditLogger>());

        // Act
        var dto = await service.CreateCaregiverAsync(CreateCaregiverRequest());

        // Assert
        dto.Email.Should().Be("caregiver@example.test");
        provisioningRequest.Should().NotBeNull();
        provisioningRequest!.Role.Should().Be(ApplicationRoles.Caregiver);
        provisioningRequest.TemporaryPassword.Should().Be("TempPass1");
        unitOfWork.Mock.Verify(work => work.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Mock.Verify(work => work.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Mock.Verify(work => work.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateClientAsync_WhenAuditFails_RollsBackAndDoesNotCommit()
    {
        // Arrange
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<User>());
        unitOfWork.Users.Setup(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync((User user, CancellationToken _) => user);
        unitOfWork.Clients.Setup(repository => repository.AddAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>())).ReturnsAsync((Client client, CancellationToken _) => client);
        var provisioning = new Mock<IIdentityProvisioningService>();
        provisioning.Setup(service => service.ProvisionUserAsync(It.IsAny<IdentityProvisioningRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityProvisioningResult.Success());
        var auditLogger = new Mock<IPhiAuditLogger>();
        auditLogger.Setup(logger => logger.LogAsync(It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("AuditUnavailable"));
        var service = new ClientOperationsService(
            unitOfWork,
            new TestCurrentUserContext(Guid.NewGuid(), new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Admin }),
            provisioning.Object,
            Mock.Of<IClientAccessEvaluator>(),
            auditLogger.Object);

        // Act
        var act = async () => await service.CreateClientAsync(CreateClientRequest());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AuditUnavailable");
        unitOfWork.Mock.Verify(work => work.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Mock.Verify(work => work.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task CreateClientAsync_WhenProvisioningFails_RollsBackAndThrowsValidationException()
    {
        // Arrange
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Users.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<User>());
        unitOfWork.Users.Setup(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync((User user, CancellationToken _) => user);
        unitOfWork.Clients.Setup(repository => repository.AddAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>())).ReturnsAsync((Client client, CancellationToken _) => client);
        var provisioning = new Mock<IIdentityProvisioningService>();
        provisioning.Setup(service => service.ProvisionUserAsync(It.IsAny<IdentityProvisioningRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityProvisioningResult.Failed("IdentityProvisioningFailed"));
        var auditLogger = new Mock<IPhiAuditLogger>();
        auditLogger.Setup(logger => logger.LogAsync(It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new ClientOperationsService(
            unitOfWork,
            new TestCurrentUserContext(Guid.NewGuid(), new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Coordinator }),
            provisioning.Object,
            Mock.Of<IClientAccessEvaluator>(),
            auditLogger.Object);

        // Act
        var act = async () => await service.CreateClientAsync(CreateClientRequest());

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("The account could not be provisioned.");
        unitOfWork.Mock.Verify(work => work.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Mock.Verify(work => work.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetClientAsync_WhenClientGrantAuthorizes_AuditsReadAndReturnsDetail()
    {
        // Arrange
        var clientUser = CreateUser(UserRole.Client);
        var client = new Client
        {
            Id = Guid.NewGuid(),
            UserId = clientUser.Id,
            DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            HourlyBillRate = 35m,
            EstimatedWeeklyHours = 20,
            User = clientUser
        };
        var callerUserId = Guid.NewGuid();
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Clients.Setup(repository => repository.GetByIdAsync(client.Id, It.IsAny<CancellationToken>())).ReturnsAsync(client);
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(client.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(clientUser);
        var evaluator = new Mock<IClientAccessEvaluator>();
        evaluator.Setup(service => service.EvaluateAsync(callerUserId, client.Id, AccessScope.Full, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientAccessEvaluationResult.Authorized());
        var auditLogger = new Mock<IPhiAuditLogger>();
        var service = new ClientOperationsService(
            unitOfWork,
            new TestCurrentUserContext(callerUserId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Client }),
            Mock.Of<IIdentityProvisioningService>(),
            evaluator.Object,
            auditLogger.Object);

        // Act
        var dto = await service.GetClientAsync(client.Id);

        // Assert
        dto.Id.Should().Be(client.Id);
        dto.HourlyBillRate.Should().Be(0m);
        dto.EstimatedWeeklyHours.Should().Be(0);
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.Action == AuditAction.Read && entry.EntityType == ProtectedResourceType.Client && entry.EntityId == client.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetClientAsync_WhenClientGrantDenies_LogsAccessDeniedAndThrowsWithoutDisclosureException()
    {
        // Arrange
        var clientUser = CreateUser(UserRole.Client);
        var client = new Client
        {
            Id = Guid.NewGuid(),
            UserId = clientUser.Id,
            DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var callerUserId = Guid.NewGuid();
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Clients.Setup(repository => repository.GetByIdAsync(client.Id, It.IsAny<CancellationToken>())).ReturnsAsync(client);
        var evaluator = new Mock<IClientAccessEvaluator>();
        evaluator.Setup(service => service.EvaluateAsync(callerUserId, client.Id, AccessScope.Full, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientAccessEvaluationResult.Denied(ClientAccessEvaluationResult.NoGrant));
        var auditLogger = new Mock<IPhiAuditLogger>();
        var service = new ClientOperationsService(
            unitOfWork,
            new TestCurrentUserContext(callerUserId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Client }),
            Mock.Of<IIdentityProvisioningService>(),
            evaluator.Object,
            auditLogger.Object);

        // Act
        var act = async () => await service.GetClientAsync(client.Id);

        // Assert
        await act.Should().ThrowAsync<ResourceAccessDeniedException>()
            .Where(exception => exception.IsPhiResource && exception.ReasonCode == "RoleInsufficient");
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.Action == AuditAction.AccessDenied && entry.EntityType == ProtectedResourceType.Client && entry.EntityId == client.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }


    [Fact]
    public async Task GetClientsAsync_WhenClientHasActiveGrant_ReturnsSelfAndGrantedClientsAndAuditsReads()
    {
        // Arrange
        var callerUserId = Guid.NewGuid();
        var selfUser = new User
        {
            Id = callerUserId,
            FirstName = "Test",
            LastName = "Client",
            Email = "self-client@example.test",
            PhoneNumber = "555-0103",
            Role = UserRole.Client,
        };
        var grantedUser = CreateUser(UserRole.Client);
        var selfClient = new Client
        {
            Id = Guid.NewGuid(),
            UserId = selfUser.Id,
            DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var grantedClient = new Client
        {
            Id = Guid.NewGuid(),
            UserId = grantedUser.Id,
            DateOfBirth = new DateTime(1985, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var grant = new ClientAccessGrant
        {
            Id = Guid.NewGuid(),
            ClientId = grantedClient.Id,
            GranteeUserId = callerUserId,
            AccessScope = AccessScope.PatientFacing,
        };
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Clients.SetupSequence(repository => repository.FindAsync(It.IsAny<Expression<Func<Client, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { selfClient })
            .ReturnsAsync(new[] { grantedClient });
        unitOfWork.ClientAccessGrants.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<ClientAccessGrant, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { grant });
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(selfUser.Id, It.IsAny<CancellationToken>())).ReturnsAsync(selfUser);
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(grantedUser.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grantedUser);
        var auditLogger = new Mock<IPhiAuditLogger>();
        var service = new ClientOperationsService(
            unitOfWork,
            new TestCurrentUserContext(callerUserId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Client }),
            Mock.Of<IIdentityProvisioningService>(),
            Mock.Of<IClientAccessEvaluator>(),
            auditLogger.Object);

        // Act
        var result = await service.GetClientsAsync(new CarePath.Contracts.Common.PagedRequest { PageNumber = 1, PageSize = 10 });

        // Assert
        result.Items.Select(client => client.Id).Should().BeEquivalentTo(new[] { selfClient.Id, grantedClient.Id });
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.Action == AuditAction.Read && entry.EntityType == ProtectedResourceType.Client),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetClientAsync_WhenCaregiverHasNoCurrentAssignment_AuditsDeniedAndThrowsWithoutDisclosureException()
    {
        // Arrange
        var caregiverUserId = Guid.NewGuid();
        var clientUser = CreateUser(UserRole.Client);
        var client = new Client
        {
            Id = Guid.NewGuid(),
            UserId = clientUser.Id,
            DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var caregiver = new Caregiver
        {
            Id = Guid.NewGuid(),
            UserId = caregiverUserId,
        };
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Clients.Setup(repository => repository.GetByIdAsync(client.Id, It.IsAny<CancellationToken>())).ReturnsAsync(client);
        unitOfWork.Caregivers.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { caregiver });
        unitOfWork.Shifts.Setup(repository => repository.ExistsAsync(It.IsAny<Expression<Func<Shift, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var auditLogger = new Mock<IPhiAuditLogger>();
        var service = new ClientOperationsService(
            unitOfWork,
            new TestCurrentUserContext(caregiverUserId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Caregiver }),
            Mock.Of<IIdentityProvisioningService>(),
            Mock.Of<IClientAccessEvaluator>(),
            auditLogger.Object);

        // Act
        var act = async () => await service.GetClientAsync(client.Id);

        // Assert
        await act.Should().ThrowAsync<ResourceAccessDeniedException>()
            .Where(exception => exception.IsPhiResource && exception.ReasonCode == "RoleInsufficient");
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.Action == AuditAction.AccessDenied && entry.EntityType == ProtectedResourceType.Client && entry.EntityId == client.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetClientsAsync_WhenClinicianHasNoRelationshipSource_ReturnsEmptyPage()
    {
        // Arrange
        var service = new ClientOperationsService(
            CreateUnitOfWork(),
            new TestCurrentUserContext(Guid.NewGuid(), new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Clinician }),
            Mock.Of<IIdentityProvisioningService>(),
            Mock.Of<IClientAccessEvaluator>(),
            Mock.Of<IPhiAuditLogger>());

        // Act
        var result = await service.GetClientsAsync(new CarePath.Contracts.Common.PagedRequest { PageNumber = 1, PageSize = 10 });

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetClientAsync_WhenClinicianHasNoRelationshipSource_AuditsDeniedAndThrowsWithoutDisclosureException()
    {
        // Arrange
        var clientUser = CreateUser(UserRole.Client);
        var client = new Client
        {
            Id = Guid.NewGuid(),
            UserId = clientUser.Id,
            DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Clients.Setup(repository => repository.GetByIdAsync(client.Id, It.IsAny<CancellationToken>())).ReturnsAsync(client);
        var auditLogger = new Mock<IPhiAuditLogger>();
        var service = new ClientOperationsService(
            unitOfWork,
            new TestCurrentUserContext(Guid.NewGuid(), new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Clinician }),
            Mock.Of<IIdentityProvisioningService>(),
            Mock.Of<IClientAccessEvaluator>(),
            auditLogger.Object);

        // Act
        var act = async () => await service.GetClientAsync(client.Id);

        // Assert
        await act.Should().ThrowAsync<ResourceAccessDeniedException>()
            .Where(exception => exception.IsPhiResource && exception.ReasonCode == "RoleInsufficient");
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.Action == AuditAction.AccessDenied && entry.EntityType == ProtectedResourceType.Client && entry.EntityId == client.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCaregiverAsync_WhenCallerIsUnauthorized_AuditsDeniedAndThrowsWithoutDisclosureException()
    {
        // Arrange
        var caregiver = new Caregiver
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
        };
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Caregivers.Setup(repository => repository.GetByIdAsync(caregiver.Id, It.IsAny<CancellationToken>())).ReturnsAsync(caregiver);
        var auditLogger = new Mock<IPhiAuditLogger>();
        var service = new CaregiverOperationsService(
            unitOfWork,
            new TestCurrentUserContext(Guid.NewGuid(), new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Caregiver }),
            Mock.Of<IIdentityProvisioningService>(),
            auditLogger.Object);

        // Act
        var act = async () => await service.GetCaregiverAsync(caregiver.Id);

        // Assert
        await act.Should().ThrowAsync<ResourceAccessDeniedException>()
            .Where(exception => exception.IsPhiResource && exception.ReasonCode == "RoleInsufficient");
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.Action == AuditAction.AccessDenied && entry.EntityType == ProtectedResourceType.Caregiver && entry.EntityId == caregiver.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void AddApplication_WhenDependenciesProvided_ResolvesOperationServicesAndValidators()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(CreateUnitOfWork().Mock.Object);
        services.AddSingleton<ICurrentUserContext>(new TestCurrentUserContext(Guid.NewGuid(), new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Admin }));
        services.AddSingleton(Mock.Of<IIdentityProvisioningService>());
        services.AddSingleton(Mock.Of<IClientAccessEvaluator>());
        services.AddSingleton(Mock.Of<IObjectAuthorizationService>());
        services.AddSingleton(Mock.Of<IPhiAuditLogger>());

        // Act
        using var provider = services.AddApplication().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        // Assert
        scope.ServiceProvider.GetRequiredService<ICaregiverOperationsService>().Should().BeOfType<CaregiverOperationsService>();
        scope.ServiceProvider.GetRequiredService<IClientOperationsService>().Should().BeOfType<ClientOperationsService>();
        scope.ServiceProvider.GetRequiredService<IIdorGuard>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IValidator<CreateClientRequest>>().Should().BeOfType<CreateClientRequestValidator>();
    }
    [Fact]
    public async Task CreateClientRequestValidator_WhenPasswordIsInvalid_DoesNotEchoAttemptedValue()
    {
        // Arrange
        var request = CreateClientRequest(temporaryPassword: "secret");
        var validator = new CreateClientRequestValidator();

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.ErrorMessage).Should().NotContain(message => message.Contains("secret", StringComparison.Ordinal));
    }

    private static MockUnitOfWork CreateUnitOfWork() => new();

    private static CreateCaregiverRequest CreateCaregiverRequest() => new()
    {
        FirstName = "Test",
        LastName = "Caregiver",
        Email = "caregiver@example.test",
        PhoneNumber = "555-0100",
        TemporaryPassword = "TempPass1",
        EmploymentType = ContractEmploymentType.W2Employee,
        HourlyPayRate = 25m,
        HireDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        MaxWeeklyHours = 40,
    };

    private static CreateClientRequest CreateClientRequest(string temporaryPassword = "TempPass1") => new()
    {
        FirstName = "Test",
        LastName = "Client",
        Email = "client@example.test",
        PhoneNumber = "555-0101",
        TemporaryPassword = temporaryPassword,
        DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ServiceType = ContractServiceType.InHomeCare,
        HourlyBillRate = 35m,
        EstimatedWeeklyHours = 20,
    };

    private static User CreateUser(UserRole role) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Test",
        LastName = "User",
        Email = $"{Guid.NewGuid():N}@example.test",
        PhoneNumber = "555-0102",
        Role = role,
    };

    private sealed record TestCurrentUserContext(
        Guid? UserId,
        IReadOnlySet<string> Roles) : ICurrentUserContext
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














