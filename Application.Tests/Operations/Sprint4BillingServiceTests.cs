using System.Linq.Expressions;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Billing;
using CarePath.Application.Billing.Services;
using CarePath.Application.Billing.Validators;
using CarePath.Application.Common.Exceptions;
using CarePath.Contracts.Billing;
using CarePath.Contracts.Common;
using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentAssertions;
using FluentValidation;
using Moq;
using DomainClient = global::CarePath.Domain.Entities.Identity.Client;
using ContractPaymentMethod = CarePath.Contracts.Enumerations.PaymentMethod;
using ContractServiceType = CarePath.Contracts.Enumerations.ServiceType;

namespace CarePath.Application.Tests.Operations;

public sealed class Sprint4BillingServiceTests
{
    private static readonly DateTime PeriodStart = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateInvoiceAsync_WhenDuplicatePeriodExists_ThrowsPhiFreeConflict()
    {
        // Arrange
        var context = CreateContext();
        context.Invoices.Setup(repository => repository.ExistsAsync(It.IsAny<Expression<Func<Invoice, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var act = async () => await context.Service.CreateInvoiceAsync(CreateInvoiceRequest(context.Client.Id));

        // Assert
        var exception = await act.Should().ThrowAsync<ResourceConflictException>();
        exception.Which.Code.Should().Be("invoice.duplicate");
        exception.Which.Message.Should().NotContain("Test Client A");
        context.ShiftBillingQuery.Verify(query => query.GetCompletedBillableShiftsAsync(
            It.IsAny<Guid>(), It.IsAny<ServiceType>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInvoiceAsync_WhenNoCompletedBillableShiftsExist_ThrowsValidationWithoutSaving()
    {
        // Arrange
        var context = CreateContext();
        context.ShiftBillingQuery.Setup(query => query.GetCompletedBillableShiftsAsync(
                context.Client.Id,
                ServiceType.InHomeCare,
                PeriodStart,
                PeriodEnd,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Shift>());

        // Act
        var act = async () => await context.Service.CreateInvoiceAsync(CreateInvoiceRequest(context.Client.Id));

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle(error => error.ErrorCode == "invoice.no_billable_shifts");
        context.Invoices.Verify(repository => repository.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Never);
        context.InvoiceLineItems.Verify(repository => repository.AddAsync(It.IsAny<InvoiceLineItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInvoiceAsync_WhenCompletedBillableShiftsExist_CreatesGenericLineItems()
    {
        // Arrange
        var context = CreateContext();
        var billableShift = CompletedShift(context.Client.Id, ServiceType.InHomeCare, PeriodStart.AddDays(1), 40m, 25m, 4m);
        var addedLineItems = new List<InvoiceLineItem>();
        context.ShiftBillingQuery.Setup(query => query.GetCompletedBillableShiftsAsync(
                context.Client.Id,
                ServiceType.InHomeCare,
                PeriodStart,
                PeriodEnd,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { billableShift });
        context.InvoiceLineItems.Setup(repository => repository.AddAsync(It.IsAny<InvoiceLineItem>(), It.IsAny<CancellationToken>()))
            .Callback<InvoiceLineItem, CancellationToken>((lineItem, _) => addedLineItems.Add(lineItem))
            .ReturnsAsync((InvoiceLineItem lineItem, CancellationToken _) => lineItem);

        // Act
        var result = await context.Service.CreateInvoiceAsync(CreateInvoiceRequest(context.Client.Id));

        // Assert
        result.LineItems.Should().ContainSingle();
        result.Subtotal.Should().Be(160m);
        result.Total.Should().Be(160m);
        result.LineItems.Single().Description.Should().Be("In-home care service");
        result.LineItems.Single().Description.Should().NotContain("Test Client A");
        addedLineItems.Should().ContainSingle(item => item.ShiftId == billableShift.Id);
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.Invoice && entry.Action == AuditAction.Create),
            It.IsAny<CancellationToken>()), Times.Once);
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.Shift && entry.Action == AuditAction.Read && entry.EntityId == billableShift.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateInvoiceAsync_WhenSaveHitsDuplicateIndex_TranslatesToPhiFreeConflict()
    {
        // Arrange
        var saveException = new InvalidOperationException("duplicate index");
        var context = CreateContext();
        var billableShift = CompletedShift(context.Client.Id, ServiceType.InHomeCare, PeriodStart.AddDays(1), 40m, 25m, 4m);
        context.ShiftBillingQuery.Setup(query => query.GetCompletedBillableShiftsAsync(
                context.Client.Id,
                ServiceType.InHomeCare,
                PeriodStart,
                PeriodEnd,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { billableShift });
        context.UnitOfWork.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(saveException);
        context.ConflictDetector.Setup(detector => detector.IsUniqueConstraintConflict(saveException, "IX_Invoices_Client_Service_Period"))
            .Returns(true);

        // Act
        var act = async () => await context.Service.CreateInvoiceAsync(CreateInvoiceRequest(context.Client.Id));

        // Assert
        var exception = await act.Should().ThrowAsync<ResourceConflictException>();
        exception.Which.Code.Should().Be("invoice.duplicate");
        context.UnitOfWork.Verify(
            work => work.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordPaymentAsync_WhenPartialPaymentRecorded_RecalculatesStatusAndBalance()
    {
        // Arrange
        var context = CreateContext();
        var invoice = Invoice(context.Client.Id, total: 100m);
        SetupInvoiceLoad(context, invoice);

        // Act
        var result = await context.Service.RecordPaymentAsync(invoice.Id, new RecordPaymentRequest
        {
            Amount = 40m,
            Method = ContractPaymentMethod.Check,
            PaymentDate = PeriodStart.AddDays(10),
        });

        // Assert
        result.Status.Should().Be(CarePath.Contracts.Enumerations.InvoiceStatus.PartiallyPaid);
        result.AmountPaid.Should().Be(40m);
        result.Balance.Should().Be(60m);
        result.PaidDate.Should().BeNull();
    }

    [Fact]
    public async Task RecordPaymentAsync_WhenPaymentSettlesInvoice_SetsPaidDate()
    {
        // Arrange
        var context = CreateContext();
        var invoice = Invoice(context.Client.Id, total: 100m);
        SetupInvoiceLoad(context, invoice);
        var paidAt = PeriodStart.AddDays(12);

        // Act
        var result = await context.Service.RecordPaymentAsync(invoice.Id, new RecordPaymentRequest
        {
            Amount = 100m,
            Method = ContractPaymentMethod.BankTransfer,
            PaymentDate = paidAt,
        });

        // Assert
        result.Status.Should().Be(CarePath.Contracts.Enumerations.InvoiceStatus.Paid);
        result.Balance.Should().Be(0m);
        result.PaidDate.Should().Be(paidAt);
    }

    [Fact]
    public async Task GetMarginSummaryAsync_WhenCompletedBillableShiftsExist_SplitsServiceLinesAndAuditsReads()
    {
        // Arrange
        var context = CreateContext();
        var inHome = CompletedShift(context.Client.Id, ServiceType.InHomeCare, PeriodStart.AddDays(1), 40m, 25m, 4m);
        var facility = CompletedShift(context.Client.Id, ServiceType.FacilityStaffing, PeriodStart.AddDays(2), 0m, 10m, 2m);
        context.ShiftBillingQuery.Setup(query => query.GetCompletedBillableShiftsAsync(PeriodStart, PeriodEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { inHome, facility });

        // Act
        var result = await context.Service.GetMarginSummaryAsync(PeriodStart, PeriodEnd);

        // Assert
        result.InHomeCare.ShiftCount.Should().Be(1);
        result.InHomeCare.TotalRevenue.Should().Be(160m);
        result.InHomeCare.TotalLaborCost.Should().Be(100m);
        result.InHomeCare.AverageHourlyGrossMargin.Should().Be(15m);
        result.FacilityStaffing.ShiftCount.Should().Be(1);
        result.FacilityStaffing.TotalRevenue.Should().Be(0m);
        result.FacilityStaffing.GrossMarginPercentage.Should().Be(0m);
        result.Overall.ShiftCount.Should().Be(2);
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.Shift && entry.Action == AuditAction.Read),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetShiftMarginsAsync_WhenAdminRequestsPage_UsesBillingQueryAndAuditsReturnedRows()
    {
        // Arrange
        var context = CreateContext();
        var shift = CompletedShift(context.Client.Id, ServiceType.InHomeCare, PeriodStart.AddDays(1), 40m, 25m, 4m);
        context.ShiftBillingQuery.Setup(query => query.GetCompletedBillableShiftPageAsync(PeriodStart, PeriodEnd, 2, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { shift }, 6));

        // Act
        var result = await context.Service.GetShiftMarginsAsync(new PagedRequest { PageNumber = 2, PageSize = 5 }, PeriodStart, PeriodEnd);

        // Assert
        result.TotalCount.Should().Be(6);
        result.Items.Should().ContainSingle(item => item.ShiftId == shift.Id && item.HourlyGrossMargin == 15m);
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.Shift && entry.Action == AuditAction.Read),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetShiftMarginsAsync_WhenCoordinatorRequestsMargins_DeniesAccess()
    {
        // Arrange
        var context = CreateContext(new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Coordinator });

        // Act
        var act = async () => await context.Service.GetShiftMarginsAsync(new PagedRequest { PageNumber = 1, PageSize = 10 }, PeriodStart, PeriodEnd);

        // Assert
        await act.Should().ThrowAsync<ResourceAccessDeniedException>();
        context.ShiftBillingQuery.Verify(query => query.GetCompletedBillableShiftPageAsync(
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInvoiceRequestValidator_WhenServiceTypeIsInvalid_ReturnsPhiFreeValidationFailure()
    {
        // Arrange
        var validator = new CreateInvoiceRequestValidator();
        var request = new CreateInvoiceRequest
        {
            ClientId = Guid.NewGuid(),
            ServiceType = (ContractServiceType)999,
            PeriodStartUtc = PeriodStart,
            PeriodEndUtc = PeriodEnd,
            DueDate = PeriodEnd.AddDays(15),
            TaxAmount = 0m,
        };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CarePath.Contracts.Billing.CreateInvoiceRequest.ServiceType));
        result.Errors.Select(error => error.ErrorMessage).Should().NotContain(message => message.Contains("Test Client A", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecordPaymentRequestValidator_WhenMethodIsInvalid_ReturnsPhiFreeValidationFailure()
    {
        // Arrange
        var validator = new RecordPaymentRequestValidator();
        var request = new RecordPaymentRequest
        {
            Amount = 10m,
            Method = (ContractPaymentMethod)999,
            PaymentDate = PeriodStart,
        };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(RecordPaymentRequest.Method));
    }

    private static TestContext CreateContext(IReadOnlySet<string>? roles = null)
    {
        var user = User(UserRole.Client);
        var client = new DomainClient
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ServiceType = ServiceType.InHomeCare,
            HourlyBillRate = 40m,
            EstimatedWeeklyHours = 20,
        };
        var currentUser = new TestCurrentUserContext(Guid.NewGuid(), roles ?? new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Admin });
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var users = new Mock<IRepository<User>>(MockBehavior.Strict);
        var clients = new Mock<IRepository<DomainClient>>(MockBehavior.Strict);
        var shifts = new Mock<IRepository<Shift>>(MockBehavior.Strict);
        var invoices = new Mock<IRepository<Invoice>>(MockBehavior.Strict);
        var lineItems = new Mock<IRepository<InvoiceLineItem>>(MockBehavior.Strict);
        var payments = new Mock<IRepository<Payment>>(MockBehavior.Strict);
        var auditLogger = new Mock<IPhiAuditLogger>(MockBehavior.Strict);
        var shiftBillingQuery = new Mock<IShiftBillingQuery>(MockBehavior.Strict);
        var conflictDetector = new Mock<IPersistenceConflictDetector>(MockBehavior.Strict);

        unitOfWork.SetupGet(work => work.Users).Returns(users.Object);
        unitOfWork.SetupGet(work => work.Clients).Returns(clients.Object);
        unitOfWork.SetupGet(work => work.Shifts).Returns(shifts.Object);
        unitOfWork.SetupGet(work => work.Invoices).Returns(invoices.Object);
        unitOfWork.SetupGet(work => work.InvoiceLineItems).Returns(lineItems.Object);
        unitOfWork.SetupGet(work => work.Payments).Returns(payments.Object);
        unitOfWork.Setup(work => work.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((operation, token) => operation(token));
        unitOfWork.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        clients.Setup(repository => repository.GetByIdAsync(client.Id, It.IsAny<CancellationToken>())).ReturnsAsync(client);
        clients.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<DomainClient, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<DomainClient>());
        users.Setup(repository => repository.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        invoices.Setup(repository => repository.ExistsAsync(It.IsAny<Expression<Func<Invoice, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        invoices.Setup(repository => repository.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invoice invoice, CancellationToken _) => invoice);
        invoices.Setup(repository => repository.UpdateAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        lineItems.Setup(repository => repository.AddAsync(It.IsAny<InvoiceLineItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvoiceLineItem lineItem, CancellationToken _) => lineItem);
        payments.Setup(repository => repository.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);
        auditLogger.Setup(logger => logger.LogAsync(It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = new BillingOperationsService(
            unitOfWork.Object,
            currentUser,
            Mock.Of<IClientAccessEvaluator>(),
            auditLogger.Object,
            shiftBillingQuery.Object,
            conflictDetector.Object);

        return new TestContext(service, client, unitOfWork, users, clients, shifts, invoices, lineItems, payments, auditLogger, shiftBillingQuery, conflictDetector);
    }

    private static void SetupInvoiceLoad(TestContext context, Invoice invoice)
    {
        context.Invoices.Setup(repository => repository.GetByIdAsync(invoice.Id, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
        context.InvoiceLineItems.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<InvoiceLineItem, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice.LineItems.ToArray());
        context.Payments.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Payment, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Payment>());
    }

    private static CreateInvoiceRequest CreateInvoiceRequest(Guid clientId) => new()
    {
        ClientId = clientId,
        ServiceType = ContractServiceType.InHomeCare,
        PeriodStartUtc = PeriodStart,
        PeriodEndUtc = PeriodEnd,
        DueDate = PeriodEnd.AddDays(15),
        TaxAmount = 0m,
    };

    private static Invoice Invoice(Guid clientId, decimal total)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            InvoiceNumber = "INV-20260701-TEST0001",
            InvoiceDate = PeriodEnd,
            DueDate = PeriodEnd.AddDays(15),
            Status = InvoiceStatus.Sent,
            ServiceType = ServiceType.InHomeCare,
            PeriodStartUtc = PeriodStart,
            PeriodEndUtc = PeriodEnd,
        };
        invoice.LineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            Invoice = invoice,
            Description = "In-home care service",
            ServiceDate = PeriodStart.AddDays(1),
            BillableHours = 2m,
            RatePerHour = total / 2m,
        });
        return invoice;
    }

    private static Shift CompletedShift(Guid clientId, ServiceType serviceType, DateTime start, decimal billRate, decimal payRate, decimal billableHours)
    {
        var actualEnd = start.AddHours((double)billableHours);
        return new Shift
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            ServiceType = serviceType,
            Status = ShiftStatus.Completed,
            ScheduledStartTime = start,
            ScheduledEndTime = start.AddHours(4),
            ActualStartTime = start,
            ActualEndTime = actualEnd,
            BillRate = billRate,
            PayRate = payRate,
            BreakMinutes = 0,
        };
    }

    private static User User(UserRole role) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Test",
        LastName = "Client A",
        Email = $"{Guid.NewGuid():N}@example.test",
        PhoneNumber = "555-0100",
        Role = role,
    };

    private sealed record TestContext(
        BillingOperationsService Service,
        DomainClient Client,
        Mock<IUnitOfWork> UnitOfWork,
        Mock<IRepository<User>> Users,
        Mock<IRepository<DomainClient>> Clients,
        Mock<IRepository<Shift>> Shifts,
        Mock<IRepository<Invoice>> Invoices,
        Mock<IRepository<InvoiceLineItem>> InvoiceLineItems,
        Mock<IRepository<Payment>> Payments,
        Mock<IPhiAuditLogger> AuditLogger,
        Mock<IShiftBillingQuery> ShiftBillingQuery,
        Mock<IPersistenceConflictDetector> ConflictDetector);

    private sealed record TestCurrentUserContext(Guid? UserId, IReadOnlySet<string> Roles) : ICurrentUserContext
    {
        public string? UserName => "test-user@example.test";

        public bool IsAuthenticated => UserId.HasValue;

        public string? CorrelationId => "test-correlation";
    }
}


