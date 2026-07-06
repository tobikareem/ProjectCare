using System.Linq.Expressions;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Transitions.Interfaces;
using CarePath.Application.Transitions.Services;
using CarePath.Application.Transitions.Validators;
using CarePath.Contracts.Common;
using CarePath.Contracts.Transitions;
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
using ContractDischargeDocumentSourceType = CarePath.Contracts.Enumerations.DischargeDocumentSourceType;
using ContractReminderChannel = CarePath.Contracts.Enumerations.ReminderChannel;
using ContractReminderType = CarePath.Contracts.Enumerations.ReminderType;
using ContractEscalationLevel = CarePath.Contracts.Enumerations.EscalationLevel;
using ContractTransitionInstructionStatus = CarePath.Contracts.Enumerations.TransitionInstructionStatus;
using ContractTransitionRiskLevel = CarePath.Contracts.Enumerations.TransitionRiskLevel;

namespace CarePath.Application.Tests.Transitions;

public sealed class TransitionsServiceTests
{
    [Fact]
    public async Task CreateDischargeDocumentAsync_WhenValid_CreatesDocumentAndDraftPlanAndReturnsMetadataOnly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var client = new Client { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), DateOfBirth = UtcDate(1940, 1, 1) };
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Clients.Setup(repository => repository.GetByIdAsync(client.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        DischargeDocument? addedDocument = null;
        TransitionPlan? addedPlan = null;
        unitOfWork.DischargeDocuments.Setup(repository => repository.AddAsync(It.IsAny<DischargeDocument>(), It.IsAny<CancellationToken>()))
            .Callback<DischargeDocument, CancellationToken>((document, _) => addedDocument = document)
            .ReturnsAsync((DischargeDocument document, CancellationToken _) => document);
        unitOfWork.TransitionPlans.Setup(repository => repository.AddAsync(It.IsAny<TransitionPlan>(), It.IsAny<CancellationToken>()))
            .Callback<TransitionPlan, CancellationToken>((plan, _) => addedPlan = plan)
            .ReturnsAsync((TransitionPlan plan, CancellationToken _) => plan);
        var auditLogger = new Mock<IPhiAuditLogger>();
        auditLogger.Setup(logger => logger.LogAsync(It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(unitOfWork, userId, auditLogger: auditLogger.Object);

        // Act
        var dto = await service.CreateDischargeDocumentAsync(CreateRequest(client.Id));

        // Assert
        dto.Id.Should().Be(addedDocument!.Id);
        dto.ClientId.Should().Be(client.Id);
        dto.SourceReference.Should().Be("DOC-123");
        typeof(DischargeDocumentDto).GetProperties().Select(property => property.Name).Should().NotContain("RawContent");
        addedDocument.RawContent.Should().Be("Medication: take synthetic tablet daily");
        addedDocument.UploadedBy.Should().Be(userId);
        addedPlan.Should().NotBeNull();
        addedPlan!.Status.Should().Be(TransitionPlanStatus.Draft);
        addedPlan.HospitalName.Should().Be("Synthetic Hospital");
        addedPlan.DischargeDate.Should().Be(UtcDate(2026, 7, 1));
        unitOfWork.Mock.Verify(work => work.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.DischargeDocument && entry.Action == AuditAction.Create),
            It.IsAny<CancellationToken>()), Times.Once);
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.TransitionPlan && entry.Action == AuditAction.Create),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateDischargeDocumentValidator_WhenSourceTypeDeferred_ReturnsStableCodeWithoutRawContent()
    {
        // Arrange
        var request = new CreateDischargeDocumentRequest
        {
            ClientId = Guid.NewGuid(),
            SourceType = ContractDischargeDocumentSourceType.PhotoUpload,
            RawContent = "sensitive discharge text",
            DischargeDate = UtcDate(2026, 7, 1),
        };
        var validator = new CreateDischargeDocumentRequestValidator();

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorCode == CreateDischargeDocumentRequestValidator.SourceTypeDeferredCode);
        result.Errors.Select(error => error.ErrorMessage).Should().NotContain(message => message.Contains("sensitive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExtractDischargeDocumentAsync_WhenDraftExists_CreatesPendingInstructionsAndAwaitsReview()
    {
        // Arrange
        var clientUser = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Synthetic",
            LastName = "Client",
            Email = "client@example.test",
            PhoneNumber = "555-0100",
            Role = UserRole.Client,
        };
        var client = new Client { Id = Guid.NewGuid(), UserId = clientUser.Id, DateOfBirth = UtcDate(1940, 1, 1) };
        var document = new DischargeDocument
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            SourceType = DischargeDocumentSourceType.PdfUpload,
            RawContent = "Medication: take synthetic tablet daily",
            Status = DischargeDocumentStatus.Pending,
            UploadedBy = Guid.NewGuid(),
            UploadedAt = UtcDate(2026, 7, 2),
        };
        var plan = new TransitionPlan
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            DischargeDocumentId = document.Id,
            HospitalName = "Synthetic Hospital",
            DischargeDate = UtcDate(2026, 7, 1),
            TransitionWindowEnd = UtcDate(2026, 7, 1),
            Status = TransitionPlanStatus.Draft,
        };
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.DischargeDocuments.Setup(repository => repository.GetByIdAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        unitOfWork.TransitionPlans.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<TransitionPlan, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });
        unitOfWork.DischargeDocuments.Setup(repository => repository.UpdateAsync(It.IsAny<DischargeDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unitOfWork.TransitionPlans.Setup(repository => repository.UpdateAsync(It.IsAny<TransitionPlan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        TransitionInstruction? addedInstruction = null;
        unitOfWork.TransitionInstructions.Setup(repository => repository.AddAsync(It.IsAny<TransitionInstruction>(), It.IsAny<CancellationToken>()))
            .Callback<TransitionInstruction, CancellationToken>((instruction, _) => addedInstruction = instruction)
            .ReturnsAsync((TransitionInstruction instruction, CancellationToken _) => instruction);
        unitOfWork.Clients.Setup(repository => repository.GetByIdAsync(client.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(client.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clientUser);
        var extraction = new Mock<IDischargeExtractionService>();
        extraction.Setup(service => service.ExtractAsync(document.RawContent!, document.SourceType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ExtractedTransitionInstruction(
                    TransitionInstructionCategory.Medication,
                    "Take synthetic tablet daily",
                    "Medication: take synthetic tablet daily",
                    0.8500m,
                    false)
            });
        var auditLogger = new Mock<IPhiAuditLogger>();
        auditLogger.Setup(logger => logger.LogAsync(It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(unitOfWork, Guid.NewGuid(), extraction.Object, auditLogger.Object);

        // Act
        var dto = await service.ExtractDischargeDocumentAsync(document.Id);

        // Assert
        document.Status.Should().Be(DischargeDocumentStatus.AwaitingReview);
        plan.Status.Should().Be(TransitionPlanStatus.PendingVerification);
        addedInstruction.Should().NotBeNull();
        addedInstruction!.Status.Should().Be(TransitionInstructionStatus.Pending);
        dto.Instructions.Should().ContainSingle(instruction => instruction.SourceText == "Medication: take synthetic tablet daily");
        unitOfWork.Mock.Verify(work => work.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtractDischargeDocumentAsync_WhenAlreadyAwaitingReview_ThrowsStableConflict()
    {
        // Arrange
        var document = new DischargeDocument
        {
            Id = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            SourceType = DischargeDocumentSourceType.PdfUpload,
            Status = DischargeDocumentStatus.AwaitingReview,
            UploadedBy = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
        };
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.DischargeDocuments.Setup(repository => repository.GetByIdAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        var service = CreateService(unitOfWork);

        // Act
        var act = async () => await service.ExtractDischargeDocumentAsync(document.Id);

        // Assert
        await act.Should().ThrowAsync<ResourceConflictException>()
            .Where(exception => exception.Code == "transition.document_already_extracted");
    }

    [Fact]
    public async Task GetPlansAsync_WhenCoordinatorRequestsDashboard_ReturnsPagedSignalsAndAuditsPlanReads()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Synthetic",
            LastName = "Client",
            Email = "client@example.test",
            PhoneNumber = "555-0100",
            Role = UserRole.Client,
        };
        var client = new Client
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DateOfBirth = UtcDate(1940, 1, 1),
        };
        var plan = new TransitionPlan
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            DischargeDocumentId = Guid.NewGuid(),
            HospitalName = "Synthetic Hospital",
            DischargeDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-2), DateTimeKind.Utc),
            TransitionWindowEnd = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(28), DateTimeKind.Utc),
            Status = TransitionPlanStatus.Active,
            RiskLevel = TransitionRiskLevel.High,
        };
        var instructions = new[]
        {
            CreateInstruction(plan.Id, TransitionInstructionStatus.Pending),
            CreateInstruction(plan.Id, TransitionInstructionStatus.Approved),
        };
        var escalations = new[]
        {
            new TransitionEscalation
            {
                Id = Guid.NewGuid(),
                TransitionPlanId = plan.Id,
                TriggerType = EscalationTriggerType.WarningSymptomsReported,
                TriggerDetails = "Warning symptom check-in recorded.",
                EscalationLevel = EscalationLevel.CoordinatorAlert,
                EscalatedAt = DateTime.UtcNow,
            },
            new TransitionEscalation
            {
                Id = Guid.NewGuid(),
                TransitionPlanId = plan.Id,
                TriggerType = EscalationTriggerType.CaregiverAlert,
                TriggerDetails = "Caregiver alert recorded.",
                EscalationLevel = EscalationLevel.CoordinatorAlert,
                EscalatedAt = DateTime.UtcNow,
                AcknowledgedAt = DateTime.UtcNow,
            },
        };
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetPagedAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { plan }, 1));
        unitOfWork.TransitionInstructions.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<TransitionInstruction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(instructions);
        unitOfWork.TransitionEscalations.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<TransitionEscalation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(escalations);
        unitOfWork.Clients.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Client, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { client });
        unitOfWork.Users.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        var auditLogger = new Mock<IPhiAuditLogger>();
        auditLogger.Setup(logger => logger.LogAsync(It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(unitOfWork, auditLogger: auditLogger.Object);

        // Act
        var result = await service.GetPlansAsync(new PagedRequest { PageNumber = 1, PageSize = 10 });

        // Assert
        result.TotalCount.Should().Be(1);
        var summary = result.Items.Should().ContainSingle().Subject;
        summary.ClientFullName.Should().Be("Synthetic Client");
        summary.PendingInstructionCount.Should().Be(1);
        summary.OpenEscalationCount.Should().Be(1);
        unitOfWork.TransitionPlans.Verify(repository => repository.GetPagedAsync(1, 10, It.IsAny<CancellationToken>()), Times.Once);
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.TransitionPlan && entry.EntityId == plan.Id && entry.Action == AuditAction.Read),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewInstructionAsync_WhenModified_UpdatesInstructionAndAuditsWrite()
    {
        // Arrange
        var plan = CreatePlan(TransitionPlanStatus.PendingVerification);
        var instruction = new TransitionInstruction
        {
            Id = Guid.NewGuid(),
            TransitionPlanId = plan.Id,
            Category = TransitionInstructionCategory.Medication,
            InstructionText = "Original instruction",
            SourceText = "Original source text",
            ConfidenceScore = 0.7000m,
            Status = TransitionInstructionStatus.Pending,
        };
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        unitOfWork.TransitionInstructions.Setup(repository => repository.GetByIdAsync(instruction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instruction);
        unitOfWork.TransitionInstructions.Setup(repository => repository.UpdateAsync(instruction, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var auditLogger = new Mock<IPhiAuditLogger>();
        auditLogger.Setup(logger => logger.LogAsync(It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(
            unitOfWork,
            roles: new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Clinician },
            auditLogger: auditLogger.Object);

        // Act
        var dto = await service.ReviewInstructionAsync(
            plan.Id,
            instruction.Id,
            new ReviewInstructionRequest
            {
                Status = ContractTransitionInstructionStatus.Modified,
                ModifiedInstructionText = "Take updated synthetic medication daily.",
                ClinicalNote = "Synthetic clinical note.",
                NeedsPharmacistReview = true,
            });

        // Assert
        instruction.Status.Should().Be(TransitionInstructionStatus.Modified);
        instruction.InstructionText.Should().Be("Take updated synthetic medication daily.");
        dto.SourceText.Should().Be("Original source text");
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.TransitionInstruction && entry.Action == AuditAction.Update),
            It.IsAny<CancellationToken>()), Times.Once);
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.TransitionInstruction && entry.Action == AuditAction.Read),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewInstructionAsync_WhenPlanIsActive_RejectsWithoutMutation()
    {
        // Arrange
        var plan = CreateActivePlan();
        var instruction = CreateInstruction(plan.Id, TransitionInstructionStatus.Approved);
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        var service = CreateService(
            unitOfWork,
            roles: new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Clinician });

        // Act
        var act = async () => await service.ReviewInstructionAsync(
            plan.Id,
            instruction.Id,
            new ReviewInstructionRequest
            {
                Status = ContractTransitionInstructionStatus.Modified,
                ModifiedInstructionText = "Changed after activation.",
            });

        // Assert
        await act.Should().ThrowAsync<ResourceConflictException>()
            .Where(exception => exception.Code == "transition.plan_not_pending_verification");
        unitOfWork.TransitionInstructions.Verify(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivatePlanAsync_WhenInstructionPending_RejectsWithoutActivation()
    {
        // Arrange
        var plan = CreatePlan(TransitionPlanStatus.PendingVerification);
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        unitOfWork.TransitionInstructions.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<TransitionInstruction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CreateInstruction(plan.Id, TransitionInstructionStatus.Pending)
            });
        var service = CreateService(
            unitOfWork,
            roles: new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Clinician });

        // Act
        var act = async () => await service.ActivatePlanAsync(plan.Id, new ActivatePlanRequest
        {
            RiskLevel = ContractTransitionRiskLevel.High,
            ConfirmESignature = true,
        });

        // Assert
        await act.Should().ThrowAsync<ResourceConflictException>()
            .Where(exception => exception.Code == "transition.instructions_pending_review");
        plan.Status.Should().Be(TransitionPlanStatus.PendingVerification);
        unitOfWork.TransitionPlans.Verify(repository => repository.UpdateAsync(It.IsAny<TransitionPlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivatePlanAsync_WhenReady_SetsESignatureFieldsAndComputesWindow()
    {
        // Arrange
        var clinicianUserId = Guid.NewGuid();
        var plan = CreatePlan(TransitionPlanStatus.PendingVerification);
        var instruction = CreateInstruction(plan.Id, TransitionInstructionStatus.Approved);
        var clientUser = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Synthetic",
            LastName = "Client",
            Email = "client@example.test",
            PhoneNumber = "555-0100",
            Role = UserRole.Client,
        };
        var client = new Client { Id = plan.ClientId, UserId = clientUser.Id, DateOfBirth = UtcDate(1940, 1, 1) };
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        unitOfWork.TransitionPlans.Setup(repository => repository.UpdateAsync(plan, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unitOfWork.TransitionInstructions.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<TransitionInstruction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { instruction });
        unitOfWork.Clients.Setup(repository => repository.GetByIdAsync(plan.ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        unitOfWork.Users.Setup(repository => repository.GetByIdAsync(client.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clientUser);
        var auditLogger = new Mock<IPhiAuditLogger>();
        auditLogger.Setup(logger => logger.LogAsync(It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(
            unitOfWork,
            clinicianUserId,
            roles: new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Clinician },
            auditLogger: auditLogger.Object);

        // Act
        var dto = await service.ActivatePlanAsync(plan.Id, new ActivatePlanRequest
        {
            RiskLevel = ContractTransitionRiskLevel.High,
            ConfirmESignature = true,
        });

        // Assert
        plan.Status.Should().Be(TransitionPlanStatus.Active);
        plan.RiskLevel.Should().Be(TransitionRiskLevel.High);
        plan.VerifiedBy.Should().Be(clinicianUserId);
        plan.VerifiedAt.Should().NotBeNull();
        plan.ActivatedAt.Should().NotBeNull();
        plan.TransitionWindowEnd.Should().Be(plan.DischargeDate.AddDays(30));
        dto.Instructions.Should().ContainSingle();
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.TransitionPlan && entry.Action == AuditAction.Update),
            It.IsAny<CancellationToken>()), Times.Once);
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.TransitionInstruction && entry.Action == AuditAction.Read),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScheduleReminderAsync_WhenPlanIsNotActive_RejectsWithStableCodeAndCreatesNoRecord()
    {
        // Arrange
        var plan = CreatePlan(TransitionPlanStatus.PendingVerification);
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        var service = CreateService(unitOfWork);

        // Act
        var act = async () => await service.ScheduleReminderAsync(plan.Id, CreateReminderRequest(DateTime.UtcNow.AddDays(1)));

        // Assert
        await act.Should().ThrowAsync<ResourceConflictException>()
            .Where(exception => exception.Code == "transition.plan_not_active");
        unitOfWork.TransitionReminders.Verify(repository => repository.AddAsync(It.IsAny<TransitionReminder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScheduleReminderAsync_WhenOutsideWindow_RejectsWithStableCode()
    {
        // Arrange
        var plan = CreateActivePlan();
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        var service = CreateService(unitOfWork);

        // Act
        var act = async () => await service.ScheduleReminderAsync(plan.Id, CreateReminderRequest(plan.TransitionWindowEnd.AddSeconds(1)));

        // Assert
        await act.Should().ThrowAsync<ResourceConflictException>()
            .Where(exception => exception.Code == "transition.outside_window");
    }

    [Fact]
    public async Task ScheduleReminderAsync_WhenActivePlanWindowExpired_RejectsWithOutsideWindowCode()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var plan = new TransitionPlan
        {
            Id = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            DischargeDocumentId = Guid.NewGuid(),
            DischargeDate = DateTime.SpecifyKind(now.Date.AddDays(-40), DateTimeKind.Utc),
            TransitionWindowEnd = DateTime.SpecifyKind(now.Date.AddDays(-10), DateTimeKind.Utc),
            Status = TransitionPlanStatus.Active,
        };
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        var service = CreateService(unitOfWork);

        // Act
        var act = async () => await service.ScheduleReminderAsync(plan.Id, CreateReminderRequest(DateTime.UtcNow.AddDays(1)));

        // Assert
        await act.Should().ThrowAsync<ResourceConflictException>()
            .Where(exception => exception.Code == "transition.outside_window");
    }

    [Fact]
    public async Task ScheduleReminderAsync_WhenInsideActiveWindow_CreatesScheduledRecordOnly()
    {
        // Arrange
        var plan = CreateActivePlan();
        var instruction = CreateInstruction(plan.Id, TransitionInstructionStatus.Approved);
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        unitOfWork.TransitionInstructions.Setup(repository => repository.GetByIdAsync(instruction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instruction);
        TransitionReminder? addedReminder = null;
        unitOfWork.TransitionReminders.Setup(repository => repository.AddAsync(It.IsAny<TransitionReminder>(), It.IsAny<CancellationToken>()))
            .Callback<TransitionReminder, CancellationToken>((reminder, _) => addedReminder = reminder)
            .ReturnsAsync((TransitionReminder reminder, CancellationToken _) => reminder);
        var service = CreateService(unitOfWork);

        // Act
        var dto = await service.ScheduleReminderAsync(plan.Id, CreateReminderRequest(DateTime.UtcNow.AddDays(1), instruction.Id));

        // Assert
        addedReminder.Should().NotBeNull();
        addedReminder!.Status.Should().Be(ReminderStatus.Scheduled);
        addedReminder.SentAt.Should().BeNull();
        dto.Id.Should().Be(addedReminder.Id);
        dto.TransitionInstructionId.Should().Be(instruction.Id);
        unitOfWork.Mock.Verify(work => work.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScheduleReminderAsync_WhenScheduledExactlyAtWindowEnd_CreatesScheduledRecord()
    {
        // Arrange
        var plan = CreateActivePlan();
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        TransitionReminder? addedReminder = null;
        unitOfWork.TransitionReminders.Setup(repository => repository.AddAsync(It.IsAny<TransitionReminder>(), It.IsAny<CancellationToken>()))
            .Callback<TransitionReminder, CancellationToken>((reminder, _) => addedReminder = reminder)
            .ReturnsAsync((TransitionReminder reminder, CancellationToken _) => reminder);
        var service = CreateService(unitOfWork);

        // Act
        var dto = await service.ScheduleReminderAsync(plan.Id, CreateReminderRequest(plan.TransitionWindowEnd));

        // Assert
        addedReminder.Should().NotBeNull();
        addedReminder!.ScheduledAt.Should().Be(plan.TransitionWindowEnd);
        dto.Status.Should().Be(CarePath.Contracts.Enumerations.ReminderStatus.Scheduled);
    }

    [Fact]
    public async Task ScheduleReminderAsync_WhenInstructionRejected_RejectsWithStableCode()
    {
        // Arrange
        var plan = CreateActivePlan();
        var instruction = CreateInstruction(plan.Id, TransitionInstructionStatus.Rejected);
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        unitOfWork.TransitionInstructions.Setup(repository => repository.GetByIdAsync(instruction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instruction);
        var service = CreateService(unitOfWork);

        // Act
        var act = async () => await service.ScheduleReminderAsync(plan.Id, CreateReminderRequest(DateTime.UtcNow.AddDays(1), instruction.Id));

        // Assert
        await act.Should().ThrowAsync<ResourceConflictException>()
            .Where(exception => exception.Code == "transition.instruction_not_schedulable");
        unitOfWork.TransitionReminders.Verify(repository => repository.AddAsync(It.IsAny<TransitionReminder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScheduleReminderAsync_WhenInstructionPending_RejectsWithStableCode()
    {
        // Arrange
        var plan = CreateActivePlan();
        var instruction = CreateInstruction(plan.Id, TransitionInstructionStatus.Pending);
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        unitOfWork.TransitionInstructions.Setup(repository => repository.GetByIdAsync(instruction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instruction);
        var service = CreateService(unitOfWork);

        // Act
        var act = async () => await service.ScheduleReminderAsync(plan.Id, CreateReminderRequest(DateTime.UtcNow.AddDays(1), instruction.Id));

        // Assert
        await act.Should().ThrowAsync<ResourceConflictException>()
            .Where(exception => exception.Code == "transition.instruction_not_schedulable");
    }

    [Fact]
    public async Task CreateCheckInAsync_WhenWarningSymptomReported_CreatesCoordinatorAlertRecordOnly()
    {
        // Arrange
        var callerUserId = Guid.NewGuid();
        var plan = CreateActivePlan();
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        TransitionCheckIn? addedCheckIn = null;
        TransitionEscalation? addedEscalation = null;
        unitOfWork.TransitionCheckIns.Setup(repository => repository.AddAsync(It.IsAny<TransitionCheckIn>(), It.IsAny<CancellationToken>()))
            .Callback<TransitionCheckIn, CancellationToken>((checkIn, _) => addedCheckIn = checkIn)
            .ReturnsAsync((TransitionCheckIn checkIn, CancellationToken _) => checkIn);
        unitOfWork.TransitionEscalations.Setup(repository => repository.AddAsync(It.IsAny<TransitionEscalation>(), It.IsAny<CancellationToken>()))
            .Callback<TransitionEscalation, CancellationToken>((escalation, _) => addedEscalation = escalation)
            .ReturnsAsync((TransitionEscalation escalation, CancellationToken _) => escalation);
        var accessEvaluator = new Mock<IClientAccessEvaluator>();
        accessEvaluator.Setup(evaluator => evaluator.EvaluateAsync(callerUserId, plan.ClientId, AccessScope.PatientFacing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientAccessEvaluationResult.Authorized());
        var service = CreateService(
            unitOfWork,
            callerUserId,
            clientAccessEvaluator: accessEvaluator.Object,
            roles: new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Client });

        // Act
        var dto = await service.CreateCheckInAsync(plan.Id, new CreateCheckInRequest
        {
            Channel = ContractReminderChannel.App,
            ResponsesJson = """{"symptom":"chest pain"}""",
        });

        // Assert
        addedCheckIn.Should().NotBeNull();
        addedCheckIn!.ContainsWarningSymptom.Should().BeTrue();
        dto.ContainsWarningSymptom.Should().BeTrue();
        typeof(TransitionCheckInDto).GetProperty("ResponsesJson").Should().BeNull();
        addedEscalation.Should().NotBeNull();
        addedEscalation!.EscalationLevel.Should().Be(EscalationLevel.CoordinatorAlert);
        addedEscalation.TriggerType.Should().Be(EscalationTriggerType.WarningSymptomsReported);
        addedEscalation.TriggerDetails.Contains("chest pain", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
    }

    [Fact]
    public async Task CreateCheckInAsync_WhenPlanIsNotActive_RejectsWithStableCode()
    {
        // Arrange
        var plan = CreatePlan(TransitionPlanStatus.PendingVerification);
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        var service = CreateService(
            unitOfWork,
            roles: new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Client });

        // Act
        var act = async () => await service.CreateCheckInAsync(plan.Id, new CreateCheckInRequest
        {
            Channel = ContractReminderChannel.App,
            ResponsesJson = """{"symptom":"none"}""",
        });

        // Assert
        await act.Should().ThrowAsync<ResourceConflictException>()
            .Where(exception => exception.Code == "transition.plan_not_active");
    }

    [Theory]
    [InlineData(TransitionPlanStatus.PendingVerification)]
    [InlineData(TransitionPlanStatus.Active)]
    public async Task CreateCheckInAsync_WhenCallerHasNoGrant_DeniesBeforePlanStateDisclosure(TransitionPlanStatus status)
    {
        // Arrange
        var callerUserId = Guid.NewGuid();
        var plan = status == TransitionPlanStatus.Active
            ? CreateActivePlan()
            : CreatePlan(status);
        if (status == TransitionPlanStatus.Active)
        {
            plan.TransitionWindowEnd = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-1), DateTimeKind.Utc);
        }

        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        var accessEvaluator = new Mock<IClientAccessEvaluator>();
        accessEvaluator.Setup(evaluator => evaluator.EvaluateAsync(callerUserId, plan.ClientId, AccessScope.PatientFacing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientAccessEvaluationResult.Denied(ClientAccessEvaluationResult.NoGrant));
        var service = CreateService(
            unitOfWork,
            callerUserId,
            clientAccessEvaluator: accessEvaluator.Object,
            roles: new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Client });

        // Act
        var act = async () => await service.CreateCheckInAsync(plan.Id, new CreateCheckInRequest
        {
            Channel = ContractReminderChannel.App,
            ResponsesJson = """{"symptom":"none"}""",
        });

        // Assert
        await act.Should().ThrowAsync<ResourceAccessDeniedException>();
    }

    [Theory]
    [InlineData(TransitionPlanStatus.PendingVerification)]
    [InlineData(TransitionPlanStatus.Active)]
    public async Task GetPatientPlanAsync_WhenCallerHasNoGrant_DeniesBeforePlanStateDisclosure(TransitionPlanStatus status)
    {
        // Arrange
        var callerUserId = Guid.NewGuid();
        var plan = status == TransitionPlanStatus.Active
            ? CreateActivePlan()
            : CreatePlan(status);
        if (status == TransitionPlanStatus.Active)
        {
            plan.TransitionWindowEnd = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-1), DateTimeKind.Utc);
        }

        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        var accessEvaluator = new Mock<IClientAccessEvaluator>();
        accessEvaluator.Setup(evaluator => evaluator.EvaluateAsync(callerUserId, plan.ClientId, AccessScope.PatientFacing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientAccessEvaluationResult.Denied(ClientAccessEvaluationResult.NoGrant));
        var service = CreateService(
            unitOfWork,
            callerUserId,
            clientAccessEvaluator: accessEvaluator.Object,
            roles: new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Client });

        // Act
        var act = async () => await service.GetPatientPlanAsync(plan.Id);

        // Assert
        await act.Should().ThrowAsync<ResourceAccessDeniedException>();
    }

    [Fact]
    public async Task CreateCheckInAsync_WhenActivePlanWindowExpired_RejectsWithStableCode()
    {
        // Arrange
        var plan = CreateActivePlan();
        plan.TransitionWindowEnd = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-1), DateTimeKind.Utc);
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        var service = CreateService(
            unitOfWork,
            roles: new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Client });

        // Act
        var act = async () => await service.CreateCheckInAsync(plan.Id, new CreateCheckInRequest
        {
            Channel = ContractReminderChannel.App,
            ResponsesJson = """{"symptom":"none"}""",
        });

        // Assert
        await act.Should().ThrowAsync<ResourceConflictException>()
            .Where(exception => exception.Code == "transition.outside_window");
    }

    [Fact]
    public async Task AcknowledgeEscalationAsync_WhenCoordinatorChoosesHigherLevel_RecordsHumanDecisionOnly()
    {
        // Arrange
        var coordinatorUserId = Guid.NewGuid();
        var escalation = new TransitionEscalation
        {
            Id = Guid.NewGuid(),
            TransitionPlanId = Guid.NewGuid(),
            TriggerType = EscalationTriggerType.WarningSymptomsReported,
            TriggerDetails = "Warning symptom check-in recorded.",
            EscalationLevel = EscalationLevel.CoordinatorAlert,
            EscalatedAt = DateTime.UtcNow,
        };
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionEscalations.Setup(repository => repository.GetByIdAsync(escalation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(escalation);
        unitOfWork.TransitionEscalations.Setup(repository => repository.UpdateAsync(escalation, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(unitOfWork, coordinatorUserId);

        // Act
        var dto = await service.AcknowledgeEscalationAsync(escalation.Id, new AcknowledgeEscalationRequest
        {
            EscalationLevel = ContractEscalationLevel.UrgentCare,
            ResolutionNote = "Coordinator documented a human decision.",
        });

        // Assert
        escalation.EscalationLevel.Should().Be(EscalationLevel.UrgentCare);
        escalation.AcknowledgedBy.Should().Be(coordinatorUserId);
        dto.ResolutionNote.Should().Be("Coordinator documented a human decision.");
    }

    [Fact]
    public async Task GetPlanForClientAsync_WhenCaregiverIsNotAssigned_DeniesAtApplicationBoundary()
    {
        // Arrange
        var callerUserId = Guid.NewGuid();
        var plan = CreateActivePlan();
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<TransitionPlan, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });
        unitOfWork.TransitionInstructions.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<TransitionInstruction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TransitionInstruction>());
        unitOfWork.Caregivers.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Caregiver { Id = Guid.NewGuid(), UserId = callerUserId } });
        unitOfWork.Shifts.Setup(repository => repository.ExistsAsync(It.IsAny<Expression<Func<Shift, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var auditLogger = new Mock<IPhiAuditLogger>();
        auditLogger.Setup(logger => logger.LogAsync(It.IsAny<PhiAuditEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(
            unitOfWork,
            callerUserId,
            auditLogger: auditLogger.Object,
            roles: new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Caregiver });

        // Act
        var act = async () => await service.GetPlanForClientAsync(plan.ClientId);

        // Assert
        await act.Should().ThrowAsync<ResourceAccessDeniedException>();
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry => entry.EntityType == ProtectedResourceType.Client && entry.EntityId == plan.ClientId && entry.Action == AuditAction.AccessDenied),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(-3, -2, ShiftStatus.Scheduled)]
    [InlineData(2, 3, ShiftStatus.Scheduled)]
    [InlineData(-1, 1, ShiftStatus.Cancelled)]
    public async Task GetPlanForClientAsync_WhenCaregiverShiftIsNotCurrentScheduledOrInProgress_DeniesAtApplicationBoundary(
        int startOffsetHours,
        int endOffsetHours,
        ShiftStatus shiftStatus)
    {
        // Arrange
        var callerUserId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var plan = CreateActivePlan();
        var now = DateTime.UtcNow;
        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            ClientId = plan.ClientId,
            CaregiverId = caregiverId,
            ScheduledStartTime = now.AddHours(startOffsetHours),
            ScheduledEndTime = now.AddHours(endOffsetHours),
            Status = shiftStatus,
        };
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.TransitionPlans.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<TransitionPlan, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });
        unitOfWork.Caregivers.Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<Caregiver, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Caregiver { Id = caregiverId, UserId = callerUserId } });
        unitOfWork.Shifts.Setup(repository => repository.ExistsAsync(
                It.Is<Expression<Func<Shift, bool>>>(expression => !expression.Compile()(shift)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var service = CreateService(
            unitOfWork,
            callerUserId,
            roles: new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Caregiver });

        // Act
        var act = async () => await service.GetPlanForClientAsync(plan.ClientId);

        // Assert
        await act.Should().ThrowAsync<ResourceAccessDeniedException>();
    }

    private static TransitionsService CreateService(
        MockUnitOfWork unitOfWork,
        Guid? userId = null,
        IDischargeExtractionService? extraction = null,
        IPhiAuditLogger? auditLogger = null,
        IClientAccessEvaluator? clientAccessEvaluator = null,
        IReadOnlySet<string>? roles = null)
    {
        return new TransitionsService(
            unitOfWork,
            new TestCurrentUserContext(
                userId ?? Guid.NewGuid(),
                roles ?? new HashSet<string>(StringComparer.Ordinal) { ApplicationRoles.Coordinator }),
            auditLogger ?? Mock.Of<IPhiAuditLogger>(),
            extraction ?? Mock.Of<IDischargeExtractionService>(),
            clientAccessEvaluator ?? CreateAuthorizedClientAccessEvaluator());
    }

    private static IClientAccessEvaluator CreateAuthorizedClientAccessEvaluator()
    {
        var evaluator = new Mock<IClientAccessEvaluator>();
        evaluator.Setup(service => service.EvaluateAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<AccessScope>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientAccessEvaluationResult.Authorized());
        return evaluator.Object;
    }

    private static MockUnitOfWork CreateUnitOfWork() => new();

    private static CreateDischargeDocumentRequest CreateRequest(Guid clientId) => new()
    {
        ClientId = clientId,
        SourceType = ContractDischargeDocumentSourceType.PdfUpload,
        RawContent = "Medication: take synthetic tablet daily",
        SourceReference = "DOC-123",
        HospitalName = "Synthetic Hospital",
        DischargeDate = UtcDate(2026, 7, 1),
    };

    private static ScheduleReminderRequest CreateReminderRequest(DateTime scheduledAt, Guid? instructionId = null) => new()
    {
        TransitionInstructionId = instructionId,
        ReminderType = ContractReminderType.Medication,
        Channel = ContractReminderChannel.App,
        ScheduledAt = scheduledAt,
    };

    private static TransitionPlan CreatePlan(TransitionPlanStatus status) => new()
    {
        Id = Guid.NewGuid(),
        ClientId = Guid.NewGuid(),
        DischargeDocumentId = Guid.NewGuid(),
        DischargeDate = UtcDate(2026, 7, 1),
        TransitionWindowEnd = UtcDate(2026, 7, 1),
        Status = status,
    };

    private static TransitionPlan CreateActivePlan()
    {
        var now = DateTime.UtcNow;
        return new TransitionPlan
        {
            Id = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            DischargeDocumentId = Guid.NewGuid(),
            DischargeDate = DateTime.SpecifyKind(now.Date.AddDays(-1), DateTimeKind.Utc),
            TransitionWindowEnd = DateTime.SpecifyKind(now.Date.AddDays(29), DateTimeKind.Utc),
            Status = TransitionPlanStatus.Active,
        };
    }

    private static TransitionInstruction CreateInstruction(Guid planId, TransitionInstructionStatus status) => new()
    {
        Id = Guid.NewGuid(),
        TransitionPlanId = planId,
        Category = TransitionInstructionCategory.Medication,
        InstructionText = "Take synthetic medication daily.",
        SourceText = "Medication: take synthetic medication daily.",
        ConfidenceScore = 0.8500m,
        Status = status,
    };

    private static DateTime UtcDate(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

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
        public Mock<IRepository<DischargeDocument>> DischargeDocuments { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<TransitionPlan>> TransitionPlans { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<TransitionInstruction>> TransitionInstructions { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<TransitionReminder>> TransitionReminders { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<TransitionCheckIn>> TransitionCheckIns { get; } = new(MockBehavior.Strict);
        public Mock<IRepository<TransitionEscalation>> TransitionEscalations { get; } = new(MockBehavior.Strict);

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
        IRepository<DischargeDocument> IUnitOfWork.DischargeDocuments => DischargeDocuments.Object;
        IRepository<TransitionPlan> IUnitOfWork.TransitionPlans => TransitionPlans.Object;
        IRepository<TransitionInstruction> IUnitOfWork.TransitionInstructions => TransitionInstructions.Object;
        IRepository<TransitionReminder> IUnitOfWork.TransitionReminders => TransitionReminders.Object;
        IRepository<TransitionCheckIn> IUnitOfWork.TransitionCheckIns => TransitionCheckIns.Object;
        IRepository<TransitionEscalation> IUnitOfWork.TransitionEscalations => TransitionEscalations.Object;
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Mock.Object.SaveChangesAsync(cancellationToken);
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Mock.Object.BeginTransactionAsync(cancellationToken);
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Mock.Object.CommitTransactionAsync(cancellationToken);
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Mock.Object.RollbackTransactionAsync(cancellationToken);
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
