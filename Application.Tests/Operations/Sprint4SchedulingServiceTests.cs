using System.Linq.Expressions;
using System.Data;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Storage;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Scheduling.Services;
using CarePath.Contracts.Scheduling;
using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Entities.Transitions;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentAssertions;
using FluentValidation;
using Moq;
using DomainClient = global::CarePath.Domain.Entities.Identity.Client;
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
    public async Task CreateShiftAsync_WhenCaregiverOmitted_CreatesOpenShiftWithoutEligibilityChecks()
    {
        // Arrange — overlap and missing certifications would both block an assigned create (D-S6-12)
        var context = CreateContext(
            existingShifts: [ExistingShift(WindowStart.AddHours(-1), WindowEnd.AddHours(1), ShiftStatus.Scheduled)],
            certifications: Array.Empty<CaregiverCertification>());

        // Act
        var result = await context.Service.CreateShiftAsync(CreateRequest(context.Client.Id, caregiverId: null));

        // Assert
        result.CaregiverId.Should().BeNull();
        result.Status.Should().Be(CarePath.Contracts.Enumerations.ShiftStatus.Scheduled);
        context.UnitOfWork.Shifts.Verify(repository => repository.AddAsync(It.Is<Shift>(shift => shift.CaregiverId == null), It.IsAny<CancellationToken>()), Times.Once);
        context.UnitOfWork.Caregivers.Verify(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        context.UnitOfWork.CaregiverCertifications.Verify(repository => repository.FindAsync(It.IsAny<Expression<Func<CaregiverCertification, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateShiftAsync_WhenCaregiverIdIsEmptyGuid_ThrowsValidationBeforeSave()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var act = async () => await context.Service.CreateShiftAsync(CreateRequest(context.Client.Id, Guid.Empty));

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        context.UnitOfWork.Shifts.Verify(repository => repository.AddAsync(It.IsAny<Shift>(), It.IsAny<CancellationToken>()), Times.Never);
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
    public async Task UpdateShiftAsync_WhenAssigningWithoutNotes_PreservesExistingNotes()
    {
        // Arrange
        var context = CreateContext(certifications: [Certification(WindowStart.Date.AddDays(10))]);
        var shift = ExistingShift(WindowStart.AddDays(3), WindowEnd.AddDays(3), ShiftStatus.Scheduled);
        shift.ClientId = context.Client.Id;
        shift.CaregiverId = null;
        shift.Notes = "Existing scheduling note.";
        context.UnitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        Shift? updatedShift = null;
        context.UnitOfWork.Shifts.Setup(repository => repository.UpdateAsync(It.IsAny<Shift>(), It.IsAny<CancellationToken>()))
            .Callback<Shift, CancellationToken>((value, _) => updatedShift = value)
            .Returns(Task.CompletedTask);
        var request = new UpdateShiftRequest
        {
            CaregiverId = context.Caregiver.Id,
            ScheduledStartUtc = WindowStart.AddDays(3),
            ScheduledEndUtc = WindowEnd.AddDays(3),
            BreakMinutes = 0,
            ServiceType = ContractServiceType.InHomeCare,
        };

        // Act
        _ = await context.Service.UpdateShiftAsync(shift.Id, request);

        // Assert
        updatedShift.Should().NotBeNull();
        updatedShift!.Notes.Should().Be("Existing scheduling note.");
    }

    [Fact]
    public async Task GetEligibleCaregiversAsync_WhenCertificationExpired_ReturnsBlockedCandidate()
    {
        // Arrange
        var context = CreateContext(certifications: [Certification(WindowStart.Date.AddDays(-1))]);
        var shift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.Scheduled);
        shift.ClientId = context.Client.Id;
        shift.CaregiverId = null;
        context.UnitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        context.UnitOfWork.Caregivers.Setup(repository => repository.GetPagedAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { context.Caregiver }, 1));
        context.UnitOfWork.Shifts.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Shift, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Shift>());

        // Act
        var result = await context.Service.GetEligibleCaregiversAsync(shift.Id, new CarePath.Contracts.Common.PagedRequest { PageNumber = 1, PageSize = 10 });

        // Assert
        var candidate = result.Items.Should().ContainSingle().Subject;
        candidate.IsAssignable.Should().BeFalse();
        candidate.BlockingReasons.Should().Contain("Credential expired or missing");
    }

    [Fact]
    public async Task GetCoverageQueueAsync_WhenBestMatchLiesBeyondFirstCandidatePage_ScansFullPopulation()
    {
        // Arrange — S6-TASK-038: the only eligible caregiver sits on candidate page 2
        var context = CreateContext();
        var openShift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.Scheduled);
        openShift.ClientId = context.Client.Id;
        openShift.CaregiverId = null;
        context.UnitOfWork.Shifts.Setup(repository => repository.GetPagedAsync(
                It.IsAny<Expression<Func<Shift, bool>>>(), 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { openShift }, 1));
        var fillerCandidates = Enumerable.Range(0, 50)
            .Select(_ => new Caregiver { Id = Guid.NewGuid(), UserId = Guid.NewGuid() })
            .ToArray();
        context.UnitOfWork.Caregivers.Setup(repository => repository.GetPagedAsync(
                It.IsAny<Expression<Func<Caregiver, bool>>>(), 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((fillerCandidates, 51));
        context.UnitOfWork.Caregivers.Setup(repository => repository.GetPagedAsync(
                It.IsAny<Expression<Func<Caregiver, bool>>>(), 2, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { context.Caregiver }, 51));
        var knownUsers = new[] { context.Caregiver.User! };
        context.UnitOfWork.Users.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<User, bool>> predicate, CancellationToken _) => knownUsers.Where(predicate.Compile()).ToArray());

        // Act
        var result = await context.Service.GetCoverageQueueAsync(new CarePath.Contracts.Common.PagedRequest { PageNumber = 1, PageSize = 10 });

        // Assert
        var row = result.Items.Should().ContainSingle().Subject;
        row.BestMatches.Should().ContainSingle().Which.Should().Be(context.Caregiver.User!.FullName);
        context.UnitOfWork.Caregivers.Verify(repository => repository.GetPagedAsync(
            It.IsAny<Expression<Func<Caregiver, bool>>>(), 2, 50, It.IsAny<CancellationToken>()), Times.Once);
        context.UnitOfWork.Caregivers.Verify(repository => repository.GetPagedAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry =>
                entry.EntityType == ProtectedResourceType.Caregiver &&
                entry.EntityId == context.Caregiver.Id &&
                entry.Action == AuditAction.Read),
            It.IsAny<CancellationToken>()), Times.Once);
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry =>
                entry.EntityType == ProtectedResourceType.Shift &&
                entry.EntityId == openShift.Id &&
                entry.Action == AuditAction.Read),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCoverageQueueAsync_WhenMultipleOpenShifts_ReusesCandidatePagesAndReturnsDeterministicTopThree()
    {
        // Arrange — four eligible candidates in repository order; every row takes the first three
        var context = CreateContext();
        var openShiftA = ExistingShift(WindowStart, WindowEnd, ShiftStatus.Scheduled);
        var openShiftB = ExistingShift(WindowStart.AddDays(3), WindowEnd.AddDays(3), ShiftStatus.Scheduled);
        openShiftA.ClientId = context.Client.Id;
        openShiftA.CaregiverId = null;
        openShiftB.ClientId = context.Client.Id;
        openShiftB.CaregiverId = null;
        context.UnitOfWork.Shifts.Setup(repository => repository.GetPagedAsync(
                It.IsAny<Expression<Func<Shift, bool>>>(), 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { openShiftA, openShiftB }, 2));
        var candidates = new[] { "Amara", "Bola", "Chidi", "Dayo" }
            .Select(firstName =>
            {
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    FirstName = firstName,
                    LastName = "Candidate",
                    Email = $"{Guid.NewGuid():N}@example.test",
                    PhoneNumber = "555-0100",
                    Role = UserRole.Caregiver,
                };
                return new Caregiver { Id = Guid.NewGuid(), UserId = user.Id, User = user };
            })
            .ToArray();
        context.UnitOfWork.Caregivers.Setup(repository => repository.GetPagedAsync(
                It.IsAny<Expression<Func<Caregiver, bool>>>(), 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((candidates, 4));
        var candidateUsers = candidates.Select(candidate => candidate.User).ToArray();
        context.UnitOfWork.Users.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<User, bool>> predicate, CancellationToken _) => candidateUsers.Where(predicate.Compile()).ToArray());
        var candidateCertifications = candidates
            .Select(candidate =>
            {
                var certification = Certification(WindowEnd.Date.AddDays(30));
                certification.CaregiverId = candidate.Id;
                return certification;
            })
            .ToArray();
        context.UnitOfWork.CaregiverCertifications.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<CaregiverCertification, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidateCertifications);

        // Act
        var result = await context.Service.GetCoverageQueueAsync(new CarePath.Contracts.Common.PagedRequest { PageNumber = 1, PageSize = 10 });

        // Assert
        result.TotalCount.Should().Be(2);
        result.PageNumber.Should().Be(1);
        result.Items.Should().HaveCount(2);
        foreach (var row in result.Items)
        {
            row.BestMatches.Should().Equal("Amara Candidate", "Bola Candidate", "Chidi Candidate");
        }

        context.UnitOfWork.Caregivers.Verify(repository => repository.GetPagedAsync(
            It.IsAny<Expression<Func<Caregiver, bool>>>(), 1, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCoverageQueueAsync_WhenNoCandidateMatches_StopsScanningAtTheOperationalPageCap()
    {
        // Arrange — an unfillable shift on a huge roster must not walk the whole table
        var context = CreateContext();
        var openShift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.Scheduled);
        openShift.ClientId = context.Client.Id;
        openShift.CaregiverId = null;
        context.UnitOfWork.Shifts.Setup(repository => repository.GetPagedAsync(
                It.IsAny<Expression<Func<Shift, bool>>>(), 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { openShift }, 1));
        var ineligiblePage = Enumerable.Range(0, 50)
            .Select(_ => new Caregiver { Id = Guid.NewGuid(), UserId = Guid.NewGuid() })
            .ToArray();
        context.UnitOfWork.Caregivers.Setup(repository => repository.GetPagedAsync(
                It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<int>(), 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ineligiblePage, 100_000));
        context.UnitOfWork.Users.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());

        // Act
        var result = await context.Service.GetCoverageQueueAsync(new CarePath.Contracts.Common.PagedRequest { PageNumber = 1, PageSize = 10 });

        // Assert
        result.Items.Should().ContainSingle().Which.BestMatches.Should().BeEmpty();
        context.UnitOfWork.Caregivers.Verify(repository => repository.GetPagedAsync(
            It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<int>(), 50, It.IsAny<CancellationToken>()), Times.Exactly(10));
    }

    [Fact]
    public async Task GetEligibleCaregiversAsync_WhenCaregiverInactive_ReturnsBlockedCandidate()
    {
        // Arrange
        var context = CreateContext();
        context.Caregiver.User!.IsActive = false;
        var shift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.Scheduled);
        shift.ClientId = context.Client.Id;
        shift.CaregiverId = null;
        context.UnitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        context.UnitOfWork.Caregivers.Setup(repository => repository.GetPagedAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { context.Caregiver }, 1));

        // Act
        var result = await context.Service.GetEligibleCaregiversAsync(shift.Id, new CarePath.Contracts.Common.PagedRequest { PageNumber = 1, PageSize = 10 });

        // Assert
        var candidate = result.Items.Should().ContainSingle().Subject;
        candidate.IsAssignable.Should().BeFalse();
        candidate.BlockingReasons.Should().Contain("Inactive caregiver");
    }

    [Fact]
    public async Task GetEligibleCaregiversAsync_WhenCandidateIsDoubleBooked_ReturnsBlockedCandidate()
    {
        // Arrange
        var context = CreateContext(existingShifts: [ExistingShift(WindowStart.AddHours(1), WindowEnd.AddHours(1), ShiftStatus.Scheduled)]);
        var shift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.Scheduled);
        shift.ClientId = context.Client.Id;
        shift.CaregiverId = null;
        context.UnitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        context.UnitOfWork.Caregivers.Setup(repository => repository.GetPagedAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { context.Caregiver }, 1));

        // Act
        var result = await context.Service.GetEligibleCaregiversAsync(shift.Id, new CarePath.Contracts.Common.PagedRequest { PageNumber = 1, PageSize = 10 });

        // Assert
        var candidate = result.Items.Should().ContainSingle().Subject;
        candidate.IsAssignable.Should().BeFalse();
        candidate.BlockingReasons.Should().Contain("Double-booked");
    }

    [Fact]
    public async Task GetEligibleCaregiversAsync_WhenWeeklyCapacityExceeded_ReturnsBlockedCandidate()
    {
        // Arrange
        var context = CreateContext();
        context.Caregiver.MaxWeeklyHours = 2;
        var shift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.Scheduled);
        shift.ClientId = context.Client.Id;
        shift.CaregiverId = null;
        context.UnitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        context.UnitOfWork.Caregivers.Setup(repository => repository.GetPagedAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { context.Caregiver }, 1));

        // Act
        var result = await context.Service.GetEligibleCaregiversAsync(shift.Id, new CarePath.Contracts.Common.PagedRequest { PageNumber = 1, PageSize = 10 });

        // Assert
        var candidate = result.Items.Should().ContainSingle().Subject;
        candidate.IsAssignable.Should().BeFalse();
        candidate.BlockingReasons.Should().Contain("Weekly capacity exceeded");
    }

    [Fact]
    public async Task GetEligibleCaregiversAsync_AuditsShiftCaregiverAndCertificationReads()
    {
        // Arrange
        var certification = Certification(WindowEnd.Date.AddDays(30));
        var context = CreateContext(certifications: [certification]);
        var shift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.Scheduled);
        shift.ClientId = context.Client.Id;
        shift.CaregiverId = null;
        context.UnitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        context.UnitOfWork.Caregivers.Setup(repository => repository.GetPagedAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { context.Caregiver }, 1));

        // Act
        _ = await context.Service.GetEligibleCaregiversAsync(shift.Id, new CarePath.Contracts.Common.PagedRequest { PageNumber = 1, PageSize = 10 });

        // Assert
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry =>
                entry.EntityType == ProtectedResourceType.Shift &&
                entry.EntityId == shift.Id &&
                entry.Action == AuditAction.Read),
            It.IsAny<CancellationToken>()), Times.Once);
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry =>
                entry.EntityType == ProtectedResourceType.Caregiver &&
                entry.EntityId == context.Caregiver.Id &&
                entry.Action == AuditAction.Read),
            It.IsAny<CancellationToken>()), Times.Once);
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry =>
                entry.EntityType == ProtectedResourceType.CaregiverCertification &&
                entry.EntityId == certification.Id &&
                entry.Action == AuditAction.Read),
            It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task GetShiftsAsync_WhenRangeProvided_QueriesHalfOpenOverlapWindowOnly()
    {
        // Arrange — D-S6-13: [fromUtc, toUtc) overlap window for the schedule board
        var context = CreateContext();
        var weekStart = new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc);
        var weekEnd = weekStart.AddDays(7);
        var inWindowShift = ExistingShift(weekStart.AddDays(1).AddHours(9), weekStart.AddDays(1).AddHours(13), ShiftStatus.Scheduled);
        inWindowShift.ClientId = context.Client.Id;
        inWindowShift.CaregiverId = context.Caregiver.Id;
        Expression<Func<Shift, bool>>? capturedPredicate = null;
        context.UnitOfWork.Shifts.Setup(repository => repository.GetPagedAsync(
                It.IsAny<Expression<Func<Shift, bool>>>(), 1, 100, It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<Shift, bool>>, int, int, CancellationToken>((predicate, _, _, _) => capturedPredicate = predicate)
            .ReturnsAsync((new[] { inWindowShift }, 1));

        // Act
        var result = await context.Service.GetShiftsAsync(
            new CarePath.Contracts.Common.PagedRequest { PageNumber = 1, PageSize = 100 },
            weekStart,
            weekEnd);

        // Assert
        result.Items.Should().ContainSingle(item => item.Id == inWindowShift.Id);
        capturedPredicate.Should().NotBeNull();
        var predicate = capturedPredicate!.Compile();
        predicate(inWindowShift).Should().BeTrue();
        predicate(ExistingShift(weekStart.AddDays(-1), weekStart, ShiftStatus.Scheduled)).Should().BeFalse("a shift ending exactly at the window start belongs to the previous week");
        predicate(ExistingShift(weekEnd, weekEnd.AddHours(4), ShiftStatus.Scheduled)).Should().BeFalse("a shift starting exactly at the window end belongs to the next week");
        predicate(ExistingShift(weekStart.AddDays(-1), weekEnd.AddDays(1), ShiftStatus.Scheduled)).Should().BeTrue("a shift spanning the whole window overlaps it");
        context.UnitOfWork.Shifts.Verify(repository => repository.GetPagedAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetShiftsAsync_WhenFromIsAfterTo_ThrowsStableRangeCodeBeforeQuery()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var act = async () => await context.Service.GetShiftsAsync(
            new CarePath.Contracts.Common.PagedRequest(),
            WindowEnd,
            WindowStart);

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle(error => error.ErrorCode == "shift.invalid_range");
        context.UnitOfWork.Shifts.Verify(repository => repository.GetPagedAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        context.UnitOfWork.Shifts.Verify(repository => repository.GetPagedAsync(
            It.IsAny<Expression<Func<Shift, bool>>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetShiftsAsync_WhenCaregiverScopedWithRange_ComposesOwnershipAndWindow()
    {
        // Arrange
        var context = CreateContext();
        var weekStart = new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc);
        var weekEnd = weekStart.AddDays(7);
        var ownShift = ExistingShift(weekStart.AddDays(2).AddHours(8), weekStart.AddDays(2).AddHours(12), ShiftStatus.Scheduled);
        ownShift.ClientId = context.Client.Id;
        ownShift.CaregiverId = context.Caregiver.Id;
        var service = new ShiftOperationsService(
            context.UnitOfWork,
            new TestCurrentUserContext(context.Caregiver.UserId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Caregiver }),
            Mock.Of<IClientAccessEvaluator>(),
            context.AuditLogger.Object);
        context.UnitOfWork.Caregivers.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { context.Caregiver });
        Expression<Func<Shift, bool>>? capturedPredicate = null;
        context.UnitOfWork.Shifts.Setup(repository => repository.GetPagedAsync(
                It.IsAny<Expression<Func<Shift, bool>>>(), 1, 20, It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<Shift, bool>>, int, int, CancellationToken>((predicate, _, _, _) => capturedPredicate = predicate)
            .ReturnsAsync((new[] { ownShift }, 1));

        // Act
        var result = await service.GetShiftsAsync(new CarePath.Contracts.Common.PagedRequest(), weekStart, weekEnd);

        // Assert
        result.Items.Should().ContainSingle(item => item.Id == ownShift.Id);
        var predicate = capturedPredicate!.Compile();
        predicate(ownShift).Should().BeTrue();
        var ownShiftOutsideWindow = ExistingShift(weekEnd.AddDays(1), weekEnd.AddDays(1).AddHours(4), ShiftStatus.Scheduled);
        ownShiftOutsideWindow.CaregiverId = context.Caregiver.Id;
        predicate(ownShiftOutsideWindow).Should().BeFalse("the window filter composes with caregiver scoping");
        var otherCaregiverInWindow = ExistingShift(weekStart.AddDays(2), weekStart.AddDays(2).AddHours(4), ShiftStatus.Scheduled);
        otherCaregiverInWindow.CaregiverId = Guid.NewGuid();
        predicate(otherCaregiverInWindow).Should().BeFalse("caregivers only see their own shifts regardless of window");
    }

    [Fact]
    public async Task GetShiftsAsync_WhenClientGrantScopesList_AuditsGrantRead()
    {
        // Arrange
        var context = CreateContext();
        var grantedUser = User(UserRole.Client);
        var grantedClient = new DomainClient
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
        context.UnitOfWork.Clients.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<DomainClient, bool>>>(), It.IsAny<CancellationToken>()))
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

    [Fact]
    public async Task CreateVisitNoteAsync_WhenCaregiverAssigned_CreatesAuditedDetail()
    {
        // Arrange
        var context = CreateContext();
        var shift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.InProgress);
        shift.ClientId = context.Client.Id;
        shift.CaregiverId = context.Caregiver.Id;
        shift.ActualStartTime = WindowStart;
        context.UnitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        context.UnitOfWork.Caregivers.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { context.Caregiver });
        context.UnitOfWork.VisitNotes.Setup(repository => repository.AddAsync(It.IsAny<VisitNote>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VisitNote note, CancellationToken _) => note);
        var service = new VisitDocumentationService(
            context.UnitOfWork,
            new TestCurrentUserContext(context.Caregiver.UserId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Caregiver }),
            Mock.Of<IClientAccessEvaluator>(),
            context.AuditLogger.Object,
            Mock.Of<IFileStorageService>());

        // Act
        var result = await service.CreateVisitNoteAsync(shift.Id, new CreateVisitNoteRequest
        {
            VisitDateTime = WindowStart,
            PersonalCare = true,
            Activities = "Test activity note",
        });

        // Assert
        result.ShiftId.Should().Be(shift.Id);
        result.VisitDateTime.Should().Be(WindowStart);
        context.AuditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.Action == AuditAction.Create && entry.EntityType == ProtectedResourceType.VisitNote && entry.EntityId == result.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddVisitPhotoAsync_WhenStorageSucceeds_ReturnsMetadataWithoutUrl()
    {
        // Arrange
        var context = CreateContext();
        var shift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.InProgress);
        shift.ClientId = context.Client.Id;
        shift.CaregiverId = context.Caregiver.Id;
        var note = new VisitNote
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            CaregiverId = context.Caregiver.Id,
            VisitDateTime = WindowStart,
        };
        context.UnitOfWork.VisitNotes.Setup(repository => repository.GetByIdAsync(note.Id, It.IsAny<CancellationToken>())).ReturnsAsync(note);
        context.UnitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        context.UnitOfWork.Caregivers.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { context.Caregiver });
        context.UnitOfWork.VisitPhotos.Setup(repository => repository.AddAsync(It.IsAny<VisitPhoto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VisitPhoto photo, CancellationToken _) => photo);
        var storage = new Mock<IFileStorageService>();
        storage.Setup(service => service.SaveAsync(It.IsAny<FileStorageWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("opaque-object-id");
        var service = new VisitDocumentationService(
            context.UnitOfWork,
            new TestCurrentUserContext(context.Caregiver.UserId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Caregiver }),
            Mock.Of<IClientAccessEvaluator>(),
            context.AuditLogger.Object,
            storage.Object);
        await using var content = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act
        var result = await service.AddVisitPhotoAsync(note.Id, "test-photo.jpg", "image/jpeg", content, "Test caption", WindowStart);

        // Assert
        result.VisitNoteId.Should().Be(note.Id);
        result.Url.Should().BeNull();
        storage.Verify(service => service.SaveAsync(It.IsAny<FileStorageWriteRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task GetVisitNotesAsync_WhenCallerOutOfScope_ReturnsEmptyPage()
    {
        // Arrange
        var context = CreateContext();
        var shift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.Completed);
        shift.ClientId = context.Client.Id;
        shift.CaregiverId = context.Caregiver.Id;
        context.UnitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        context.UnitOfWork.Caregivers.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Caregiver>());
        var service = new VisitDocumentationService(
            context.UnitOfWork,
            new TestCurrentUserContext(Guid.NewGuid(), new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Caregiver }),
            Mock.Of<IClientAccessEvaluator>(),
            context.AuditLogger.Object,
            Mock.Of<IFileStorageService>());

        // Act
        var result = await service.GetVisitNotesAsync(shift.Id, new CarePath.Contracts.Common.PagedRequest { PageNumber = 1, PageSize = 10 });

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        context.UnitOfWork.VisitNotes.Verify(repository => repository.GetPagedAsync(It.IsAny<Expression<Func<VisitNote, bool>>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateVisitNoteAsync_WhenVisitDateProvided_PreservesUtcTimestamp()
    {
        // Arrange
        var context = CreateContext();
        var shift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.InProgress);
        shift.ClientId = context.Client.Id;
        shift.CaregiverId = context.Caregiver.Id;
        context.UnitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        context.UnitOfWork.Caregivers.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { context.Caregiver });
        VisitNote? savedNote = null;
        context.UnitOfWork.VisitNotes.Setup(repository => repository.AddAsync(It.IsAny<VisitNote>(), It.IsAny<CancellationToken>()))
            .Callback<VisitNote, CancellationToken>((note, _) => savedNote = note)
            .ReturnsAsync((VisitNote note, CancellationToken _) => note);
        var service = new VisitDocumentationService(
            context.UnitOfWork,
            new TestCurrentUserContext(context.Caregiver.UserId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Caregiver }),
            Mock.Of<IClientAccessEvaluator>(),
            context.AuditLogger.Object,
            Mock.Of<IFileStorageService>());
        var documentedAt = WindowStart.AddMinutes(15);

        // Act
        _ = await service.CreateVisitNoteAsync(shift.Id, new CreateVisitNoteRequest
        {
            VisitDateTime = documentedAt,
            PersonalCare = true,
        });

        // Assert
        savedNote.Should().NotBeNull();
        savedNote!.VisitDateTime.Should().Be(documentedAt);
    }

    [Fact]
    public async Task CreateVisitNoteAsync_WhenActiveTransitionPlanCoversVisitDate_LinksTransitionPlan()
    {
        // Arrange
        var context = CreateContext();
        var shift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.InProgress);
        shift.ClientId = context.Client.Id;
        shift.CaregiverId = context.Caregiver.Id;
        var transitionPlan = new TransitionPlan
        {
            Id = Guid.NewGuid(),
            ClientId = context.Client.Id,
            DischargeDocumentId = Guid.NewGuid(),
            DischargeDate = WindowStart.AddDays(-1),
            TransitionWindowEnd = WindowStart.AddDays(29),
            Status = TransitionPlanStatus.Active,
            ActivatedAt = WindowStart.AddHours(-1),
        };
        context.UnitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        context.UnitOfWork.Caregivers.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { context.Caregiver });
        context.UnitOfWork.TransitionPlans.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<TransitionPlan, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { transitionPlan });
        VisitNote? savedNote = null;
        context.UnitOfWork.VisitNotes.Setup(repository => repository.AddAsync(It.IsAny<VisitNote>(), It.IsAny<CancellationToken>()))
            .Callback<VisitNote, CancellationToken>((note, _) => savedNote = note)
            .ReturnsAsync((VisitNote note, CancellationToken _) => note);
        var service = new VisitDocumentationService(
            context.UnitOfWork,
            new TestCurrentUserContext(context.Caregiver.UserId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Caregiver }),
            Mock.Of<IClientAccessEvaluator>(),
            context.AuditLogger.Object,
            Mock.Of<IFileStorageService>());

        // Act
        _ = await service.CreateVisitNoteAsync(shift.Id, new CreateVisitNoteRequest
        {
            VisitDateTime = WindowStart,
            PersonalCare = true,
        });

        // Assert
        savedNote.Should().NotBeNull();
        savedNote!.TransitionPlanId.Should().Be(transitionPlan.Id);
    }

    [Fact]
    public async Task AddVisitPhotoAsync_WhenTimestampIsNotUtc_RejectsWithoutStorageWrite()
    {
        // Arrange
        var context = CreateContext();
        var shift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.InProgress);
        shift.ClientId = context.Client.Id;
        shift.CaregiverId = context.Caregiver.Id;
        var note = new VisitNote { Id = Guid.NewGuid(), ShiftId = shift.Id, CaregiverId = context.Caregiver.Id, VisitDateTime = WindowStart };
        context.UnitOfWork.VisitNotes.Setup(repository => repository.GetByIdAsync(note.Id, It.IsAny<CancellationToken>())).ReturnsAsync(note);
        context.UnitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        context.UnitOfWork.Caregivers.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { context.Caregiver });
        var storage = new Mock<IFileStorageService>();
        var service = new VisitDocumentationService(
            context.UnitOfWork,
            new TestCurrentUserContext(context.Caregiver.UserId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Caregiver }),
            Mock.Of<IClientAccessEvaluator>(),
            context.AuditLogger.Object,
            storage.Object);
        await using var content = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act
        var act = async () => await service.AddVisitPhotoAsync(note.Id, "test.jpg", "image/jpeg", content, null, DateTime.SpecifyKind(WindowStart, DateTimeKind.Local));

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        storage.Verify(service => service.SaveAsync(It.IsAny<FileStorageWriteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddVisitPhotoAsync_WhenMetadataSaveFails_DeletesStoredObject()
    {
        // Arrange
        var context = CreateContext();
        var shift = ExistingShift(WindowStart, WindowEnd, ShiftStatus.InProgress);
        shift.ClientId = context.Client.Id;
        shift.CaregiverId = context.Caregiver.Id;
        var note = new VisitNote { Id = Guid.NewGuid(), ShiftId = shift.Id, CaregiverId = context.Caregiver.Id, VisitDateTime = WindowStart };
        context.UnitOfWork.VisitNotes.Setup(repository => repository.GetByIdAsync(note.Id, It.IsAny<CancellationToken>())).ReturnsAsync(note);
        context.UnitOfWork.Shifts.Setup(repository => repository.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        context.UnitOfWork.Caregivers.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { context.Caregiver });
        context.UnitOfWork.VisitPhotos.Setup(repository => repository.AddAsync(It.IsAny<VisitPhoto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("metadata failed"));
        var storage = new Mock<IFileStorageService>();
        storage.Setup(service => service.SaveAsync(It.IsAny<FileStorageWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("opaque-object-id");
        storage.Setup(service => service.DeleteAsync("opaque-object-id", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new VisitDocumentationService(
            context.UnitOfWork,
            new TestCurrentUserContext(context.Caregiver.UserId, new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Caregiver }),
            Mock.Of<IClientAccessEvaluator>(),
            context.AuditLogger.Object,
            storage.Object);
        await using var content = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act
        var act = async () => await service.AddVisitPhotoAsync(note.Id, "test.jpg", "image/jpeg", content, null, WindowStart);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        storage.Verify(service => service.DeleteAsync("opaque-object-id", It.IsAny<CancellationToken>()), Times.Once);
    }
    private static TestContext CreateContext(
        IReadOnlyList<Shift>? existingShifts = null,
        IReadOnlyList<CaregiverCertification>? certifications = null)
    {
        var unitOfWork = new MockUnitOfWork();
        var currentUserId = Guid.NewGuid();
        var clientUser = User(UserRole.Client);
        var caregiverUser = User(UserRole.Caregiver);
        var client = new DomainClient
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
        unitOfWork.Shifts.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Shift, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Shift, bool>> predicate, CancellationToken _) => (existingShifts ?? Array.Empty<Shift>()).Where(predicate.Compile()).ToArray());
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

    private static CreateShiftRequest CreateRequest(Guid clientId, Guid? caregiverId) => new()
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
        DomainClient Client,
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

        public MockUnitOfWork()
        {
            Mock.Setup(work => work.ExecuteInTransactionAsync(
                    It.IsAny<IsolationLevel>(),
                    It.IsAny<Func<CancellationToken, Task>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Mock.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            TransitionPlans.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<TransitionPlan, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<TransitionPlan>());
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


