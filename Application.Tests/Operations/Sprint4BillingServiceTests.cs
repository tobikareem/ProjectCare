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
    public async Task PreviewInvoiceAsync_ReturnsPagedRowsFullSetAggregatesExclusionCountsAndToken()
    {
        // Arrange — three eligible (page size 2) + one excluded row
        var context = CreateContext();
        var rowA = EligibleRow(context.Client.Id, PeriodStart.AddDays(1), 40m, 25m, 4m);
        var rowB = EligibleRow(context.Client.Id, PeriodStart.AddDays(2), 40m, 25m, 3m);
        var rowC = EligibleRow(context.Client.Id, PeriodStart.AddDays(3), 40m, 25m, 2m);
        var excluded = rowA with
        {
            ShiftId = Guid.NewGuid(),
            Reason = BillingExclusionReason.MissingActualTime,
            ActualStartUtc = null,
            ActualEndUtc = null,
        };
        SetupEligibility(context, rowA, rowB, rowC, excluded);

        // Act
        var result = await context.Service.PreviewInvoiceAsync(PreviewRequest(context.Client.Id, pageNumber: 1, pageSize: 2));

        // Assert
        result.Rows.Should().HaveCount(2, "rows are paged");
        result.EligibleShiftCount.Should().Be(3, "aggregates cover the full set");
        result.TotalBillableHours.Should().Be(9m);
        result.Subtotal.Should().Be(360m);
        result.ExclusionCounts.Should().ContainSingle(count =>
            count.Reason == CarePath.Contracts.Enumerations.BillingExclusionReason.MissingActualTime && count.Count == 1);
        result.PreviewToken.Should().NotBeNullOrEmpty();
        result.Rows.Should().OnlyContain(row => row.CaregiverDisplayName == "Amara Candidate" && row.QualificationLabel == "CNA");
    }

    [Fact]
    public async Task PreviewInvoiceAsync_WhenNoEligibleRows_IssuesNoToken()
    {
        // Arrange
        var context = CreateContext();
        var excluded = EligibleRow(context.Client.Id, PeriodStart.AddDays(1), 40m, 25m, 4m) with
        {
            Reason = BillingExclusionReason.MissingBillRate,
            BillRate = 0m,
        };
        SetupEligibility(context, excluded);

        // Act
        var result = await context.Service.PreviewInvoiceAsync(PreviewRequest(context.Client.Id));

        // Assert
        result.EligibleShiftCount.Should().Be(0);
        result.PreviewToken.Should().BeEmpty("an empty/all-excluded preview cannot generate an invoice");
    }

    [Fact]
    public async Task PreviewInvoiceAsync_RoundsEachLineAwayFromZeroBeforeSumming()
    {
        // Arrange — 1.25h × 33.33 = 41.6625 → 41.66; twice = 83.32 (sum of rounded lines,
        // not round-of-sum 83.33)
        var context = CreateContext();
        var rowA = EligibleRow(context.Client.Id, PeriodStart.AddDays(1), 33.33m, 20m, 1.25m);
        var rowB = EligibleRow(context.Client.Id, PeriodStart.AddDays(2), 33.33m, 20m, 1.25m);
        SetupEligibility(context, rowA, rowB);

        // Act
        var result = await context.Service.PreviewInvoiceAsync(PreviewRequest(context.Client.Id));

        // Assert
        result.Rows.Should().OnlyContain(row => row.LineTotal == 41.66m);
        result.Subtotal.Should().Be(83.32m);
    }

    [Fact]
    public async Task CreateInvoiceAsync_WhenDuplicatePeriodExists_ThrowsPhiFreeConflict()
    {
        // Arrange — valid token first; the exact-period duplicate is detected afterwards
        var context = CreateContext();
        var token = await IssueTokenAsync(context, EligibleRow(context.Client.Id, PeriodStart.AddDays(1), 40m, 25m, 4m));
        context.Invoices.Setup(repository => repository.ExistsAsync(It.IsAny<Expression<Func<Invoice, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var act = async () => await context.Service.CreateInvoiceAsync(CreateInvoiceRequest(context.Client.Id, token));

        // Assert
        var exception = await act.Should().ThrowAsync<ResourceConflictException>();
        exception.Which.Code.Should().Be("invoice.duplicate");
        exception.Which.Message.Should().NotContain("Test Client A");
        context.Invoices.Verify(repository => repository.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInvoiceAsync_WhenEligibilityDrainsToEmptyAfterPreview_ThrowsValidationWithoutSaving()
    {
        // Arrange
        var context = CreateContext();
        var token = await IssueTokenAsync(context, EligibleRow(context.Client.Id, PeriodStart.AddDays(1), 40m, 25m, 4m));
        SetupEligibility(context);

        // Act
        var act = async () => await context.Service.CreateInvoiceAsync(CreateInvoiceRequest(context.Client.Id, token));

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle(error => error.ErrorCode == "invoice.no_billable_shifts");
        context.Invoices.Verify(repository => repository.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Never);
        context.InvoiceLineItems.Verify(repository => repository.AddAsync(It.IsAny<InvoiceLineItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInvoiceAsync_WithCurrentPreviewToken_CreatesRoundedLineItemsInsideTransaction()
    {
        // Arrange
        var context = CreateContext();
        var row = EligibleRow(context.Client.Id, PeriodStart.AddDays(1), 40m, 25m, 4m);
        var token = await IssueTokenAsync(context, row);
        var addedLineItems = new List<InvoiceLineItem>();
        context.InvoiceLineItems.Setup(repository => repository.AddAsync(It.IsAny<InvoiceLineItem>(), It.IsAny<CancellationToken>()))
            .Callback<InvoiceLineItem, CancellationToken>((lineItem, _) => addedLineItems.Add(lineItem))
            .ReturnsAsync((InvoiceLineItem lineItem, CancellationToken _) => lineItem);

        // Act
        var result = await context.Service.CreateInvoiceAsync(CreateInvoiceRequest(context.Client.Id, token));

        // Assert
        result.LineItems.Should().ContainSingle();
        result.Subtotal.Should().Be(160m);
        result.Total.Should().Be(160m);
        result.LineItems.Single().Description.Should().Be("In-home care service");
        addedLineItems.Should().ContainSingle(item => item.ShiftId == row.ShiftId);
        context.UnitOfWork.Verify(
            work => work.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.Invoice && entry.Action == AuditAction.Create),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    public async Task CreateInvoiceAsync_WithMissingOrTamperedToken_ThrowsSanitizedPreviewStale(string token)
    {
        // Arrange
        var context = CreateContext();

        // Act
        var act = async () => await context.Service.CreateInvoiceAsync(CreateInvoiceRequest(context.Client.Id, token));

        // Assert
        if (token.Length == 0)
        {
            await act.Should().ThrowAsync<ValidationException>("an absent token fails validation with invoice.preview_required");
        }
        else
        {
            var exception = await act.Should().ThrowAsync<ResourceConflictException>();
            exception.Which.Code.Should().Be("invoice.preview_stale");
            exception.Which.Message.Should().NotContain("Test Client A");
        }

        context.Invoices.Verify(repository => repository.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInvoiceAsync_WhenTokenExpired_ThrowsPreviewStale()
    {
        // Arrange
        var context = CreateContext();
        var token = await IssueTokenAsync(context, EligibleRow(context.Client.Id, PeriodStart.AddDays(1), 40m, 25m, 4m));
        context.PreviewTokens.ExpireAllTokens();

        // Act
        var act = async () => await context.Service.CreateInvoiceAsync(CreateInvoiceRequest(context.Client.Id, token));

        // Assert
        (await act.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be("invoice.preview_stale");
    }

    [Fact]
    public async Task CreateInvoiceAsync_WhenTokenBelongsToDifferentSelection_ThrowsPreviewStale()
    {
        // Arrange — token issued for this client, replayed against another client
        var context = CreateContext();
        var token = await IssueTokenAsync(context, EligibleRow(context.Client.Id, PeriodStart.AddDays(1), 40m, 25m, 4m));
        var otherClientId = Guid.NewGuid();

        // Act
        var act = async () => await context.Service.CreateInvoiceAsync(CreateInvoiceRequest(otherClientId, token));

        // Assert
        (await act.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be("invoice.preview_stale");
        context.Clients.Verify(repository => repository.GetByIdAsync(otherClientId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInvoiceAsync_WhenBillableInputsChangedAfterPreview_ThrowsPreviewStaleInsteadOfSilentlyChangingTotals()
    {
        // Arrange — same shift set, but the rate changed between preview and create
        var context = CreateContext();
        var row = EligibleRow(context.Client.Id, PeriodStart.AddDays(1), 40m, 25m, 4m);
        var token = await IssueTokenAsync(context, row);
        SetupEligibility(context, row with { BillRate = 45m });

        // Act
        var act = async () => await context.Service.CreateInvoiceAsync(CreateInvoiceRequest(context.Client.Id, token));

        // Assert
        (await act.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be("invoice.preview_stale");
        context.Invoices.Verify(repository => repository.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInvoiceAsync_WhenSaveHitsDuplicateIndex_TranslatesToPhiFreeConflict()
    {
        // Arrange
        var saveException = new InvalidOperationException("duplicate index");
        var context = CreateContext();
        var token = await IssueTokenAsync(context, EligibleRow(context.Client.Id, PeriodStart.AddDays(1), 40m, 25m, 4m));
        context.UnitOfWork.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(saveException);
        context.ConflictDetector.Setup(detector => detector.IsUniqueConstraintConflict(saveException, "IX_Invoices_Client_Service_Period"))
            .Returns(true);

        // Act
        var act = async () => await context.Service.CreateInvoiceAsync(CreateInvoiceRequest(context.Client.Id, token));

        // Assert
        (await act.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be("invoice.duplicate");
    }

    [Fact]
    public async Task CreateInvoiceAsync_WhenConcurrentCreateWinsShiftUniqueIndex_ThrowsPreviewStale()
    {
        // Arrange — the D-S6-18 concurrency guard: the ShiftId unique index race surfaces as a
        // sanitized refresh/re-preview conflict
        var saveException = new InvalidOperationException("unique shift line");
        var context = CreateContext();
        var token = await IssueTokenAsync(context, EligibleRow(context.Client.Id, PeriodStart.AddDays(1), 40m, 25m, 4m));
        context.UnitOfWork.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(saveException);
        context.ConflictDetector.Setup(detector => detector.IsUniqueConstraintConflict(saveException, "IX_Invoices_Client_Service_Period"))
            .Returns(false);
        context.ConflictDetector.Setup(detector => detector.IsUniqueConstraintConflict(saveException, "UX_InvoiceLineItems_ShiftId_NotNull"))
            .Returns(true);

        // Act
        var act = async () => await context.Service.CreateInvoiceAsync(CreateInvoiceRequest(context.Client.Id, token));

        // Assert
        (await act.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be("invoice.preview_stale");
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

        var eligibilityQuery = new Mock<IBillingEligibilityQuery>(MockBehavior.Strict);
        var previewTokens = new FakePreviewTokenService();
        var service = new BillingOperationsService(
            unitOfWork.Object,
            currentUser,
            Mock.Of<IClientAccessEvaluator>(),
            auditLogger.Object,
            eligibilityQuery.Object,
            previewTokens,
            shiftBillingQuery.Object,
            conflictDetector.Object);

        return new TestContext(service, client, unitOfWork, users, clients, shifts, invoices, lineItems, payments, auditLogger, shiftBillingQuery, eligibilityQuery, previewTokens, conflictDetector);
    }

    private static BillingEligibilityRow EligibleRow(
        Guid clientId,
        DateTime start,
        decimal billRate,
        decimal payRate,
        decimal hours,
        ServiceType serviceType = ServiceType.InHomeCare)
    {
        return new BillingEligibilityRow(
            Guid.NewGuid(),
            clientId,
            "Test Client A",
            Guid.NewGuid(),
            "Amara Candidate",
            "CNA",
            serviceType,
            ShiftStatus.Completed,
            start,
            start.AddHours((double)hours),
            start,
            start.AddHours((double)hours),
            0,
            billRate,
            payRate,
            new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
            BillingExclusionReason.Eligible,
            null);
    }

    private static InvoicePreviewRequest PreviewRequest(Guid clientId, int pageNumber = 1, int pageSize = 20) => new()
    {
        ClientId = clientId,
        ServiceType = ContractServiceType.InHomeCare,
        PeriodStartUtc = PeriodStart,
        PeriodEndUtc = PeriodEnd,
        PageNumber = pageNumber,
        PageSize = pageSize,
    };

    private static void SetupEligibility(TestContext context, params BillingEligibilityRow[] rows)
    {
        context.EligibilityQuery.Setup(query => query.GetPeriodRowsAsync(
                context.Client.Id,
                ServiceType.InHomeCare,
                PeriodStart,
                PeriodEnd,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);
    }

    private static async Task<string> IssueTokenAsync(TestContext context, params BillingEligibilityRow[] rows)
    {
        SetupEligibility(context, rows);
        var preview = await context.Service.PreviewInvoiceAsync(PreviewRequest(context.Client.Id));
        return preview.PreviewToken;
    }

    private static void SetupInvoiceLoad(TestContext context, Invoice invoice)
    {
        context.Invoices.Setup(repository => repository.GetByIdAsync(invoice.Id, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
        context.InvoiceLineItems.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<InvoiceLineItem, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice.LineItems.ToArray());
        context.Payments.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Payment, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Payment>());
    }

    private static CreateInvoiceRequest CreateInvoiceRequest(Guid clientId, string previewToken) => new()
    {
        ClientId = clientId,
        ServiceType = ContractServiceType.InHomeCare,
        PeriodStartUtc = PeriodStart,
        PeriodEndUtc = PeriodEnd,
        DueDate = PeriodEnd.AddDays(15),
        TaxAmount = 0m,
        PreviewToken = previewToken,
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
        Mock<IBillingEligibilityQuery> EligibilityQuery,
        FakePreviewTokenService PreviewTokens,
        Mock<IPersistenceConflictDetector> ConflictDetector);

    /// <summary>
    /// Deterministic in-memory stand-in for the opaque token service: real bind/verify
    /// semantics (unknown, tampered, or expired tokens return null) without cryptography.
    /// </summary>
    internal sealed class FakePreviewTokenService : IInvoicePreviewTokenService
    {
        private readonly Dictionary<string, InvoicePreviewFingerprint> issued = new(StringComparer.Ordinal);
        private bool expired;

        public TimeSpan Lifetime => TimeSpan.FromMinutes(15);

        public string Protect(InvoicePreviewFingerprint fingerprint, out DateTime expiresAtUtc)
        {
            expiresAtUtc = DateTime.UtcNow.Add(Lifetime);
            var token = Guid.NewGuid().ToString("N");
            issued[token] = fingerprint;
            return token;
        }

        public InvoicePreviewFingerprint? Unprotect(string token)
        {
            if (expired || string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            return issued.TryGetValue(token, out var fingerprint) ? fingerprint : null;
        }

        public void ExpireAllTokens() => expired = true;
    }

    private sealed record TestCurrentUserContext(Guid? UserId, IReadOnlySet<string> Roles) : ICurrentUserContext
    {
        public string? UserName => "test-user@example.test";

        public bool IsAuthenticated => UserId.HasValue;

        public string? CorrelationId => "test-correlation";
    }
}


