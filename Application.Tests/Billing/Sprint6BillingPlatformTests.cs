using System.Linq.Expressions;
using System.Reflection;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Billing;
using CarePath.Application.Billing.Services;
using CarePath.Application.Billing.Validators;
using CarePath.Application.Common.Exceptions;
using CarePath.Contracts.Billing;
using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentAssertions;
using FluentValidation;
using Moq;
using ContractBillingCorrectiveDestination = CarePath.Contracts.Enumerations.BillingCorrectiveDestination;
using ContractBillingExclusionReason = CarePath.Contracts.Enumerations.BillingExclusionReason;
using ContractBillingReconciliationReason = CarePath.Contracts.Enumerations.BillingReconciliationReason;
using ContractBillingTimeCorrectionReason = CarePath.Contracts.Enumerations.BillingTimeCorrectionReason;

namespace CarePath.Application.Tests.Billing;

/// <summary>
/// D-S6-18 backend platform tests: contract allowlists/denylists, enum parity and precedence,
/// shared billing math, qualification labels, and the reconciliation service behavior.
/// </summary>
public sealed class Sprint6BillingPlatformTests
{
    private static readonly DateTime Window = new(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

    // ── Contract shapes ────────────────────────────────────────────────────────────────

    [Fact]
    public void InvoicePreviewRowDto_CarriesExactlyTheApprovedFields()
    {
        var names = typeof(InvoicePreviewRowDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        names.Should().BeEquivalentTo(new[]
        {
            "ServiceDateUtc", "ServiceStartUtc", "ServiceEndUtc",
            "BillableHours", "BillRate", "LineTotal",
            "CaregiverDisplayName", "QualificationLabel",
        });
    }

    [Theory]
    [InlineData(typeof(InvoicePreviewRowDto))]
    [InlineData(typeof(InvoicePreviewResponseDto))]
    [InlineData(typeof(BillingReconciliationRowDto))]
    [InlineData(typeof(BillingReconciliationDetailDto))]
    [InlineData(typeof(BillingResolutionRecordDto))]
    [InlineData(typeof(BillingReconciliationSearchResponseDto))]
    public void BillingDtos_NeverExposeForbiddenIdentityFinancialOrClinicalFields(Type dtoType)
    {
        var forbiddenMarkers = new[]
        {
            "CaregiverId", "PayRate", "CostPerHour", "Cost", "Margin", "GrossProfit",
            "Latitude", "Longitude", "Gps", "PhoneNumber", "Email", "Address",
            "Diagnosis", "Medical", "Allerg", "VisitNote", "ClientCondition", "Concern",
            "CertificationNumber", "DateOfBirth", "RawContent", "SourceText", "ResponsesJson",
        };

        var offending = dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => forbiddenMarkers.Any(marker =>
                property.Name.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .Select(property => $"{dtoType.Name}.{property.Name}")
            .ToArray();

        offending.Should().BeEmpty();
    }

    [Fact]
    public void BillingEnums_MirrorDomainValuesExactly_AndPrecedenceIsAscending()
    {
        foreach (var name in Enum.GetNames<BillingExclusionReason>())
        {
            ((int)Enum.Parse<ContractBillingExclusionReason>(name))
                .Should().Be((int)Enum.Parse<BillingExclusionReason>(name));
        }

        foreach (var name in Enum.GetNames<BillingReconciliationReason>())
        {
            ((int)Enum.Parse<ContractBillingReconciliationReason>(name))
                .Should().Be((int)Enum.Parse<BillingReconciliationReason>(name));
        }

        // The locked one-reason-per-shift precedence, pinned by numeric order.
        Enum.GetValues<BillingExclusionReason>().OrderBy(value => (int)value).Should().Equal(
            BillingExclusionReason.AlreadyInvoiced,
            BillingExclusionReason.NonBillableResolved,
            BillingExclusionReason.CancelledOrNoShow,
            BillingExclusionReason.NotCompleted,
            BillingExclusionReason.MissingActualTime,
            BillingExclusionReason.InvalidBillableTime,
            BillingExclusionReason.MissingBillRate,
            BillingExclusionReason.Eligible);
    }

    // ── Shared billing math ────────────────────────────────────────────────────────────

    [Fact]
    public void BillingMath_ComputesBreakAwareHoursAndAwayFromZeroRounding()
    {
        var row = Row(BillingExclusionReason.Eligible) with
        {
            ActualStartUtc = Window,
            ActualEndUtc = Window.AddMinutes(255),
            BreakMinutes = 30,
            BillRate = 33.33m,
        };

        BillingMath.BillableHours(row).Should().Be(3.75m, "225 minutes after the break");
        BillingMath.LineTotal(row).Should().Be(124.99m, "3.75 × 33.33 = 124.9875 → away-from-zero 124.99");
        BillingMath.RoundCurrency(0.005m).Should().Be(0.01m, "midpoints round away from zero");
    }

    [Fact]
    public void BillingMath_ReturnsNullForNonComputableTimeAndEstimatesFromScheduleWhenPossible()
    {
        var missingTime = Row(BillingExclusionReason.MissingActualTime) with
        {
            ActualStartUtc = null,
            ActualEndUtc = null,
        };
        BillingMath.BillableHours(missingTime).Should().BeNull();
        BillingMath.EstimatedValue(missingTime).Should().Be(160m, "4 scheduled hours × 40");

        var missingRate = Row(BillingExclusionReason.MissingBillRate) with { BillRate = 0m };
        BillingMath.EstimatedValue(missingRate).Should().BeNull("no rate means no calculable value");
    }

    [Fact]
    public void BillingMath_AppliesRiskAndAgingRulesDeterministically()
    {
        BillingMath.IsRevenueAtRisk(Row(BillingExclusionReason.MissingActualTime)).Should().BeTrue();
        BillingMath.IsRevenueAtRisk(Row(BillingExclusionReason.CancelledOrNoShow)).Should().BeFalse();
        BillingMath.IsRevenueAtRisk(Row(BillingExclusionReason.AlreadyInvoiced)).Should().BeFalse();
        BillingMath.IsRevenueAtRisk(Row(BillingExclusionReason.NonBillableResolved)).Should().BeFalse();

        var row = Row(BillingExclusionReason.MissingBillRate);
        BillingMath.AgeDays(row, Window.AddDays(3)).Should().Be(3);
        BillingMath.IsAgedRisk(row, Window.AddDays(BillingMath.AgedRiskThresholdDays - 1)).Should().BeFalse();
        BillingMath.IsAgedRisk(row, Window.AddDays(BillingMath.AgedRiskThresholdDays)).Should().BeTrue();
    }

    // ── Qualification labels ───────────────────────────────────────────────────────────

    [Fact]
    public void QualificationLabel_UsesCanonicalOrderServiceDateValidityAndOmitsTraining()
    {
        var serviceDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var label = BillingQualification.LabelFor(
            new[]
            {
                (CertificationType.CNA, serviceDate.AddYears(-1), serviceDate.AddYears(1)),
                (CertificationType.RN, serviceDate.AddYears(-2), serviceDate.AddYears(2)),
                (CertificationType.CPR, serviceDate.AddYears(-1), serviceDate.AddYears(1)),
                (CertificationType.LPN, serviceDate.AddYears(-1), serviceDate.AddDays(-1)),
                (CertificationType.GNA, serviceDate.AddDays(1), serviceDate.AddYears(1)),
            },
            serviceDate);

        label.Should().Be("RN, CNA", "canonical order, expired LPN and not-yet-issued GNA excluded, CPR training omitted");
        BillingQualification.LabelFor([], serviceDate).Should().Be("Caregiver");
    }

    // ── Reconciliation service ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_WhenCallerLacksBillingRole_DeniesBeforeQuery()
    {
        var context = CreateContext(role: ApplicationRoles.Clinician);

        var act = async () => await context.Service.SearchAsync(SearchRequest());

        await act.Should().ThrowAsync<ResourceAccessDeniedException>();
        context.EligibilityQuery.Verify(query => query.SearchReconciliationAsync(
            It.IsAny<ReconciliationSearchCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WhenWindowExceeds92Days_RejectsWithStableCode()
    {
        var context = CreateContext();
        var request = new BillingReconciliationSearchRequest
        {
            PeriodStartUtc = Window,
            PeriodEndUtc = Window.AddDays(93),
        };

        var act = async () => await context.Service.SearchAsync(request);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().Contain(error => error.ErrorCode == "reconciliation.range_too_large");
    }

    [Fact]
    public async Task SearchAsync_MapsRowsWithKpisAndAuditsReturnedShiftReads()
    {
        var context = CreateContext();
        var row = Row(BillingExclusionReason.MissingActualTime) with
        {
            ActualStartUtc = null,
            ActualEndUtc = null,
        };
        context.EligibilityQuery.Setup(query => query.SearchReconciliationAsync(
                It.Is<ReconciliationSearchCriteria>(criteria =>
                    criteria.PageNumber == 1 && criteria.PageSize == 20 && !criteria.AgedRiskOnly),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReconciliationPage(
                new[] { row },
                TotalCount: 5,
                new ReconciliationKpiTotals(5, 800m, 2, 320m)));

        var result = await context.Service.SearchAsync(SearchRequest());

        result.TotalCount.Should().Be(5);
        result.Kpis.UnresolvedCount.Should().Be(5);
        result.Kpis.RevenueAtRiskValue.Should().Be(800m);
        var mapped = result.Rows.Should().ContainSingle().Subject;
        mapped.ShiftId.Should().Be(row.ShiftId);
        mapped.Reason.Should().Be(ContractBillingExclusionReason.MissingActualTime);
        mapped.IsRevenueAtRisk.Should().BeTrue();
        mapped.CorrectiveDestination.Should().Be(ContractBillingCorrectiveDestination.ShiftTimeCorrection);
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.Shift
                && entry.EntityId == row.ShiftId && entry.Action == AuditAction.Read),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsOwningInvoiceLinkAndAppendOnlyHistoryWithSupersededFlags()
    {
        var context = CreateContext();
        var owningInvoiceId = Guid.NewGuid();
        var row = Row(BillingExclusionReason.AlreadyInvoiced) with { OwningInvoiceId = owningInvoiceId };
        SetupRow(context, row);
        var original = Resolution(row.ShiftId, BillingReconciliationReason.TrainingShift, Now.AddDays(-2));
        var reopen = Resolution(row.ShiftId, BillingReconciliationReason.Reopened, Now.AddDays(-1));
        reopen.SupersedesResolutionId = original.Id;
        SetupHistory(context, row.ShiftId, reopen, original);

        var result = await context.Service.GetDetailAsync(row.ShiftId);

        result.Row.OwningInvoiceId.Should().Be(owningInvoiceId);
        result.Row.CorrectiveDestination.Should().Be(ContractBillingCorrectiveDestination.InvoiceDetail);
        result.ResolutionHistory.Should().HaveCount(2);
        result.ResolutionHistory[0].Reason.Should().Be(ContractBillingReconciliationReason.Reopened);
        result.ResolutionHistory[0].IsSuperseded.Should().BeFalse();
        result.ResolutionHistory[1].IsSuperseded.Should().BeTrue("the reopen superseded it");
        result.ResolutionHistory[1].ResolvedByDisplayName.Should().Be("Test Resolver");
    }

    [Fact]
    public async Task GetDetailAsync_AuditsExactlyOneShiftReadRegardlessOfHistoryDepth()
    {
        var context = CreateContext();
        var row = Row(BillingExclusionReason.NonBillableResolved);
        SetupRow(context, row);
        var original = Resolution(row.ShiftId, BillingReconciliationReason.TrainingShift, Now.AddDays(-2));
        var reopen = Resolution(row.ShiftId, BillingReconciliationReason.Reopened, Now.AddDays(-1));
        reopen.SupersedesResolutionId = original.Id;
        SetupHistory(context, row.ShiftId, reopen, original);

        _ = await context.Service.GetDetailAsync(row.ShiftId);

        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.Shift
                && entry.EntityId == row.ShiftId && entry.Action == AuditAction.Read),
            It.IsAny<CancellationToken>()), Times.Once);
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_AppendsResolutionWithActorAndAuditsInsideTransaction()
    {
        var context = CreateContext();
        var row = Row(BillingExclusionReason.CancelledOrNoShow);
        SetupRow(context, row);
        SetupHistory(context, row.ShiftId);
        BillingReconciliationResolution? appended = null;
        context.Store.Setup(store => store.AppendAsync(It.IsAny<BillingReconciliationResolution>(), It.IsAny<CancellationToken>()))
            .Callback<BillingReconciliationResolution, CancellationToken>((resolution, _) => appended = resolution)
            .Returns(Task.CompletedTask);

        _ = await context.Service.ResolveAsync(row.ShiftId, new ResolveNonBillableRequest
        {
            Reason = ContractBillingReconciliationReason.TrainingShift,
            Note = "  Orientation shift  ",
        });

        appended.Should().NotBeNull();
        appended!.Reason.Should().Be(BillingReconciliationReason.TrainingShift);
        appended.ResolvedByUserId.Should().Be(context.UserId);
        appended.Note.Should().Be("Orientation shift");
        appended.SupersedesResolutionId.Should().BeNull();
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.Shift
                && entry.EntityId == row.ShiftId && entry.Action == AuditAction.Update),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(BillingExclusionReason.AlreadyInvoiced, "reconciliation.already_invoiced")]
    [InlineData(BillingExclusionReason.NonBillableResolved, "reconciliation.already_resolved")]
    public async Task ResolveAsync_RejectsInvoicedOrAlreadyResolvedRows(BillingExclusionReason reason, string expectedCode)
    {
        var context = CreateContext();
        var row = Row(reason);
        SetupRow(context, row);

        var act = async () => await context.Service.ResolveAsync(row.ShiftId, new ResolveNonBillableRequest
        {
            Reason = ContractBillingReconciliationReason.DataEntryError,
        });

        (await act.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be(expectedCode);
        context.Store.Verify(store => store.AppendAsync(It.IsAny<BillingReconciliationResolution>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_RejectsTheReservedReopenedReason()
    {
        var context = CreateContext();

        var act = async () => await context.Service.ResolveAsync(Guid.NewGuid(), new ResolveNonBillableRequest
        {
            Reason = ContractBillingReconciliationReason.Reopened,
        });

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().Contain(error => error.ErrorCode == "reconciliation.invalid_reason");
    }

    [Fact]
    public async Task ReopenAsync_AppendsSupersedingRecordAndNeverEditsHistory()
    {
        var context = CreateContext();
        var row = Row(BillingExclusionReason.NonBillableResolved);
        SetupRow(context, row);
        var original = Resolution(row.ShiftId, BillingReconciliationReason.GoodwillService, Now.AddDays(-1));
        context.Store.Setup(store => store.GetLatestAsync(row.ShiftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(original);
        SetupHistory(context, row.ShiftId, original);
        BillingReconciliationResolution? appended = null;
        context.Store.Setup(store => store.AppendAsync(It.IsAny<BillingReconciliationResolution>(), It.IsAny<CancellationToken>()))
            .Callback<BillingReconciliationResolution, CancellationToken>((resolution, _) => appended = resolution)
            .Returns(Task.CompletedTask);

        _ = await context.Service.ReopenAsync(row.ShiftId, new ReopenResolutionRequest());

        appended!.Reason.Should().Be(BillingReconciliationReason.Reopened);
        appended.SupersedesResolutionId.Should().Be(original.Id);
        context.Store.Verify(store => store.AppendAsync(It.IsAny<BillingReconciliationResolution>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReopenAsync_WhenNothingIsResolved_ReturnsStableConflict()
    {
        var context = CreateContext();
        var row = Row(BillingExclusionReason.CancelledOrNoShow);
        SetupRow(context, row);
        context.Store.Setup(store => store.GetLatestAsync(row.ShiftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingReconciliationResolution?)null);

        var act = async () => await context.Service.ReopenAsync(row.ShiftId, new ReopenResolutionRequest());

        (await act.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be("reconciliation.not_resolved");
    }

    [Fact]
    public async Task CorrectTimeAsync_UpdatesShiftWindowAndAuditsWithReasonEnumNameOnly()
    {
        var context = CreateContext();
        var row = Row(BillingExclusionReason.MissingActualTime) with { ActualStartUtc = null, ActualEndUtc = null };
        SetupRow(context, row);
        SetupHistory(context, row.ShiftId);
        var shift = new CarePath.Domain.Entities.Scheduling.Shift
        {
            Id = row.ShiftId,
            ClientId = row.ClientId,
            Status = ShiftStatus.Completed,
            ScheduledStartTime = row.ScheduledStartUtc,
            ScheduledEndTime = row.ScheduledEndUtc,
        };
        context.Shifts.Setup(repository => repository.GetByIdAsync(row.ShiftId, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        context.Shifts.Setup(repository => repository.UpdateAsync(shift, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _ = await context.Service.CorrectTimeAsync(row.ShiftId, new CorrectShiftTimeRequest
        {
            ActualStartUtc = Window,
            ActualEndUtc = Window.AddHours(4),
            BreakMinutes = 30,
            Reason = ContractBillingTimeCorrectionReason.MissedCheckOut,
        });

        shift.ActualStartTime.Should().Be(Window);
        shift.ActualEndTime.Should().Be(Window.AddHours(4));
        shift.BreakMinutes.Should().Be(30);
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.Shift
                && entry.EntityId == row.ShiftId
                && entry.Action == AuditAction.Update
                && entry.Attributes != null
                && entry.Attributes["TimeCorrectionReason"] == "MissedCheckOut"
                && entry.Attributes.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CorrectTimeAsync_WhenRowIsNotTimeCorrectable_ReturnsStableConflict()
    {
        var context = CreateContext();
        var row = Row(BillingExclusionReason.MissingBillRate);
        SetupRow(context, row);

        var act = async () => await context.Service.CorrectTimeAsync(row.ShiftId, new CorrectShiftTimeRequest
        {
            ActualStartUtc = Window,
            ActualEndUtc = Window.AddHours(4),
            BreakMinutes = 0,
            Reason = ContractBillingTimeCorrectionReason.DataEntryError,
        });

        (await act.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be("reconciliation.not_time_correctable");
        context.Shifts.Verify(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CorrectTimeAsync_WhenWindowExceedsMaxPlausibleLength_FailsValidation()
    {
        var context = CreateContext();

        var act = async () => await context.Service.CorrectTimeAsync(Guid.NewGuid(), new CorrectShiftTimeRequest
        {
            ActualStartUtc = Window,
            ActualEndUtc = Window.AddHours(CorrectShiftTimeRequestValidator.MaxWindowHours + 1),
            BreakMinutes = 0,
            Reason = ContractBillingTimeCorrectionReason.MissedCheckOut,
        });

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().Contain(error => error.ErrorCode == "reconciliation.window_implausible");
    }

    [Fact]
    public async Task CorrectTimeAsync_WhenWindowIsFarFromScheduled_ReturnsStableConflictAndNeverTouchesTheShift()
    {
        var context = CreateContext();
        var row = Row(BillingExclusionReason.MissingActualTime) with { ActualStartUtc = null, ActualEndUtc = null };
        SetupRow(context, row);

        var act = async () => await context.Service.CorrectTimeAsync(row.ShiftId, new CorrectShiftTimeRequest
        {
            ActualStartUtc = row.ScheduledStartUtc.AddDays(-3),
            ActualEndUtc = row.ScheduledStartUtc.AddDays(-3).AddHours(4),
            BreakMinutes = 0,
            Reason = ContractBillingTimeCorrectionReason.DataEntryError,
        });

        (await act.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be("reconciliation.window_implausible");
        context.Shifts.Verify(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────────

    private static BillingEligibilityRow Row(BillingExclusionReason reason) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Test Client A",
        Guid.NewGuid(),
        "Amara Candidate",
        "CNA",
        ServiceType.InHomeCare,
        ShiftStatus.Completed,
        Window,
        Window.AddHours(4),
        Window,
        Window.AddHours(4),
        0,
        40m,
        25m,
        Now,
        reason,
        null);

    private static BillingReconciliationSearchRequest SearchRequest() => new()
    {
        PeriodStartUtc = Window.AddDays(-10),
        PeriodEndUtc = Window.AddDays(10),
    };

    private static BillingReconciliationResolution Resolution(
        Guid shiftId,
        BillingReconciliationReason reason,
        DateTime resolvedAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        ShiftId = shiftId,
        Reason = reason,
        ResolvedByUserId = ResolverUserId,
        ResolvedAtUtc = resolvedAtUtc,
    };

    private static readonly Guid ResolverUserId = Guid.NewGuid();

    private static void SetupRow(TestContext context, BillingEligibilityRow row)
    {
        context.EligibilityQuery.Setup(query => query.GetShiftRowAsync(row.ShiftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);
    }

    private static void SetupHistory(TestContext context, Guid shiftId, params BillingReconciliationResolution[] history)
    {
        context.Store.Setup(store => store.GetHistoryAsync(shiftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(history);
    }

    private sealed record TestContext(
        BillingReconciliationService Service,
        Guid UserId,
        Mock<IBillingEligibilityQuery> EligibilityQuery,
        Mock<IBillingReconciliationStore> Store,
        Mock<IRepository<CarePath.Domain.Entities.Scheduling.Shift>> Shifts,
        Mock<IPhiAuditLogger> AuditLogger);

    private static TestContext CreateContext(string role = ApplicationRoles.Coordinator)
    {
        var userId = Guid.NewGuid();
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var shifts = new Mock<IRepository<CarePath.Domain.Entities.Scheduling.Shift>>(MockBehavior.Strict);
        var users = new Mock<IRepository<User>>(MockBehavior.Strict);
        unitOfWork.SetupGet(work => work.Shifts).Returns(shifts.Object);
        unitOfWork.SetupGet(work => work.Users).Returns(users.Object);
        unitOfWork.Setup(work => work.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((operation, token) => operation(token));
        unitOfWork.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        users.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new User
                {
                    Id = ResolverUserId,
                    FirstName = "Test",
                    LastName = "Resolver",
                    Email = "resolver@example.test",
                    PhoneNumber = "555-0100",
                    Role = UserRole.Coordinator,
                },
            });
        var auditLogger = new Mock<IPhiAuditLogger>();
        auditLogger.Setup(logger => logger.LogAsync(It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var eligibilityQuery = new Mock<IBillingEligibilityQuery>(MockBehavior.Strict);
        var store = new Mock<IBillingReconciliationStore>(MockBehavior.Strict);
        var service = new BillingReconciliationService(
            unitOfWork.Object,
            new TestCurrentUserContext(userId, new HashSet<string>(StringComparer.Ordinal) { role }),
            auditLogger.Object,
            eligibilityQuery.Object,
            store.Object);

        return new TestContext(service, userId, eligibilityQuery, store, shifts, auditLogger);
    }

    private sealed record TestCurrentUserContext(Guid? UserId, IReadOnlySet<string> Roles) : ICurrentUserContext
    {
        public string? UserName => "test-user@example.test";

        public bool IsAuthenticated => UserId.HasValue;

        public string? CorrelationId => "test-correlation";
    }
}
