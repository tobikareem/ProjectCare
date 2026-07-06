using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Common.Mapping;
using CarePath.Application.Transitions.Interfaces;
using CarePath.Application.Transitions.Validators;
using CarePath.Contracts.Common;
using CarePath.Contracts.Transitions;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Transitions;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentValidation;

namespace CarePath.Application.Transitions.Services;

public sealed class TransitionsService : ITransitionsService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ICurrentUserContext currentUser;
    private readonly IPhiAuditLogger auditLogger;
    private readonly IDischargeExtractionService extractionService;
    private readonly IValidator<CreateDischargeDocumentRequest> createDocumentValidator;
    private readonly IValidator<ReviewInstructionRequest> reviewInstructionValidator;
    private readonly IValidator<ActivatePlanRequest> activatePlanValidator;
    private readonly IValidator<ScheduleReminderRequest> scheduleReminderValidator;
    private readonly IClientAccessEvaluator clientAccessEvaluator;
    private readonly IValidator<CreateCheckInRequest> createCheckInValidator;
    private readonly IValidator<AcknowledgeEscalationRequest> acknowledgeEscalationValidator;

    public TransitionsService(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        IPhiAuditLogger auditLogger,
        IDischargeExtractionService extractionService,
        IClientAccessEvaluator clientAccessEvaluator,
        IValidator<CreateDischargeDocumentRequest>? createDocumentValidator = null,
        IValidator<ReviewInstructionRequest>? reviewInstructionValidator = null,
        IValidator<ActivatePlanRequest>? activatePlanValidator = null,
        IValidator<ScheduleReminderRequest>? scheduleReminderValidator = null,
        IValidator<CreateCheckInRequest>? createCheckInValidator = null,
        IValidator<AcknowledgeEscalationRequest>? acknowledgeEscalationValidator = null)
    {
        this.unitOfWork = unitOfWork;
        this.currentUser = currentUser;
        this.auditLogger = auditLogger;
        this.extractionService = extractionService;
        this.clientAccessEvaluator = clientAccessEvaluator;
        this.createDocumentValidator = createDocumentValidator ?? new CreateDischargeDocumentRequestValidator();
        this.reviewInstructionValidator = reviewInstructionValidator ?? new ReviewInstructionRequestValidator();
        this.activatePlanValidator = activatePlanValidator ?? new ActivatePlanRequestValidator();
        this.scheduleReminderValidator = scheduleReminderValidator ?? new ScheduleReminderRequestValidator();
        this.createCheckInValidator = createCheckInValidator ?? new CreateCheckInRequestValidator();
        this.acknowledgeEscalationValidator = acknowledgeEscalationValidator ?? new AcknowledgeEscalationRequestValidator();
    }

    public async Task<DischargeDocumentDto> CreateDischargeDocumentAsync(
        CreateDischargeDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCoordinator();
        await createDocumentValidator.ValidateAndThrowAsync(request, cancellationToken);
        _ = await GetClientAsync(request.ClientId, cancellationToken);

        var now = DateTime.UtcNow;
        var document = new DischargeDocument
        {
            ClientId = request.ClientId,
            SourceType = (DischargeDocumentSourceType)(int)request.SourceType,
            RawContent = request.RawContent,
            SourceReference = TrimToNull(request.SourceReference),
            Status = DischargeDocumentStatus.Pending,
            UploadedBy = currentUser.UserId ?? throw new ResourceAccessDeniedException("Unauthenticated", isPhiResource: true),
            UploadedAt = now,
        };

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.DischargeDocuments.AddAsync(document, cancellationToken);
            var plan = new TransitionPlan
            {
                ClientId = document.ClientId,
                DischargeDocumentId = document.Id,
                HospitalName = TrimToNull(request.HospitalName),
                DischargeDate = request.DischargeDate,
                TransitionWindowEnd = request.DischargeDate,
                Status = TransitionPlanStatus.Draft,
                RiskLevel = TransitionRiskLevel.Medium,
            };
            await unitOfWork.TransitionPlans.AddAsync(plan, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await AuditAsync(ProtectedResourceType.DischargeDocument, document.Id, AuditAction.Create, cancellationToken);
            await AuditAsync(ProtectedResourceType.TransitionPlan, plan.Id, AuditAction.Create, cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return document.ToDto();
    }

    public async Task<DischargeDocumentDto> GetDischargeDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        EnsureCoordinatorOrClinician();
        var document = await GetDocumentAsync(documentId, cancellationToken);
        await AuditAsync(ProtectedResourceType.DischargeDocument, document.Id, AuditAction.Read, cancellationToken);

        return document.ToDto();
    }

    public async Task<DischargeDocumentContentDto> GetDischargeDocumentContentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        EnsureCoordinatorOrClinician();
        var document = await GetDocumentAsync(documentId, cancellationToken);
        await AuditAsync(ProtectedResourceType.DischargeDocument, document.Id, AuditAction.Read, cancellationToken);

        return document.ToContentDto();
    }

    public async Task<TransitionPlanClinicalDto> ExtractDischargeDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        EnsureCoordinator();
        var document = await GetDocumentAsync(documentId, cancellationToken);
        if (document.Status == DischargeDocumentStatus.AwaitingReview)
        {
            throw new ResourceConflictException("transition.document_already_extracted", "Document has already been extracted.");
        }

        var plans = await unitOfWork.TransitionPlans.FindAsync(
            plan => plan.DischargeDocumentId == document.Id,
            cancellationToken);
        var plan = plans.SingleOrDefault()
            ?? throw new ResourceNotFoundException(isPhiResource: true);
        if (plan.Status != TransitionPlanStatus.Draft)
        {
            throw new ResourceConflictException("transition.document_already_extracted", "Document has already been extracted.");
        }

        document.Status = DischargeDocumentStatus.Extracting;
        var extractedInstructions = await extractionService.ExtractAsync(
            document.RawContent ?? string.Empty,
            document.SourceType,
            cancellationToken);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.DischargeDocuments.UpdateAsync(document, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var instructions = extractedInstructions.Select(extracted => new TransitionInstruction
            {
                TransitionPlanId = plan.Id,
                Category = extracted.Category,
                InstructionText = extracted.InstructionText,
                SourceText = extracted.SourceText,
                ConfidenceScore = extracted.ConfidenceScore,
                NeedsPharmacistReview = extracted.NeedsPharmacistReview,
                Status = TransitionInstructionStatus.Pending,
            }).ToArray();

            foreach (var instruction in instructions)
            {
                await unitOfWork.TransitionInstructions.AddAsync(instruction, cancellationToken);
            }

            document.Status = DischargeDocumentStatus.AwaitingReview;
            plan.Status = TransitionPlanStatus.PendingVerification;
            await unitOfWork.DischargeDocuments.UpdateAsync(document, cancellationToken);
            await unitOfWork.TransitionPlans.UpdateAsync(plan, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            await AuditAsync(ProtectedResourceType.DischargeDocument, document.Id, AuditAction.Update, cancellationToken);
            await AuditAsync(ProtectedResourceType.TransitionPlan, plan.Id, AuditAction.Update, cancellationToken);
            foreach (var instruction in instructions)
            {
                await AuditAsync(ProtectedResourceType.TransitionInstruction, instruction.Id, AuditAction.Create, cancellationToken);
            }

            await unitOfWork.CommitTransactionAsync(cancellationToken);

            var client = await GetClientAsync(plan.ClientId, cancellationToken);
            var user = await GetUserAsync(client.UserId, cancellationToken);
            foreach (var instruction in instructions)
            {
                await AuditAsync(ProtectedResourceType.TransitionInstruction, instruction.Id, AuditAction.Read, cancellationToken);
            }

            return plan.ToClinicalDto(client, user, instructions);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PagedResult<TransitionPlanSummaryDto>> GetPlansAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCoordinatorOrClinician();

        var (plans, totalCount) = await unitOfWork.TransitionPlans.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            cancellationToken);
        var planIds = plans.Select(plan => plan.Id).ToArray();
        var clientIds = plans.Select(plan => plan.ClientId).Distinct().ToArray();

        var instructions = planIds.Length == 0
            ? Array.Empty<TransitionInstruction>()
            : await unitOfWork.TransitionInstructions.FindAsync(
                instruction => planIds.Contains(instruction.TransitionPlanId),
                cancellationToken);
        var escalations = planIds.Length == 0
            ? Array.Empty<TransitionEscalation>()
            : await unitOfWork.TransitionEscalations.FindAsync(
                escalation => planIds.Contains(escalation.TransitionPlanId),
                cancellationToken);
        var clients = clientIds.Length == 0
            ? Array.Empty<Client>()
            : await unitOfWork.Clients.FindAsync(
                client => clientIds.Contains(client.Id),
                cancellationToken);
        var userIds = clients.Select(client => client.UserId).Distinct().ToArray();
        var users = userIds.Length == 0
            ? Array.Empty<User>()
            : await unitOfWork.Users.FindAsync(
                user => userIds.Contains(user.Id),
                cancellationToken);

        var clientsById = clients.ToDictionary(client => client.Id);
        var usersById = users.ToDictionary(user => user.Id);
        var pendingInstructionCounts = instructions
            .Where(instruction => instruction.Status == TransitionInstructionStatus.Pending)
            .GroupBy(instruction => instruction.TransitionPlanId)
            .ToDictionary(group => group.Key, group => group.Count());
        var openEscalationCounts = escalations
            .Where(escalation => escalation.AcknowledgedAt is null)
            .GroupBy(escalation => escalation.TransitionPlanId)
            .ToDictionary(group => group.Key, group => group.Count());

        var summaries = new List<TransitionPlanSummaryDto>(plans.Count);
        foreach (var plan in plans)
        {
            if (!clientsById.TryGetValue(plan.ClientId, out var client)
                || !usersById.TryGetValue(client.UserId, out var user))
            {
                continue;
            }

            await AuditAsync(ProtectedResourceType.TransitionPlan, plan.Id, AuditAction.Read, cancellationToken);
            summaries.Add(plan.ToSummaryDto(
                client,
                user,
                pendingInstructionCounts.GetValueOrDefault(plan.Id),
                openEscalationCounts.GetValueOrDefault(plan.Id)));
        }

        return new PagedResult<TransitionPlanSummaryDto>
        {
            Items = summaries,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<TransitionInstructionClinicalDto> ReviewInstructionAsync(
        Guid planId,
        Guid instructionId,
        ReviewInstructionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureClinician();
        await reviewInstructionValidator.ValidateAndThrowAsync(request, cancellationToken);
        var plan = await GetPlanAsync(planId, cancellationToken);
        if (plan.Status != TransitionPlanStatus.PendingVerification)
        {
            throw new ResourceConflictException("transition.plan_not_pending_verification", "transition.plan_not_pending_verification");
        }

        var instruction = await GetInstructionAsync(instructionId, cancellationToken);
        if (instruction.TransitionPlanId != planId)
        {
            throw new ResourceNotFoundException(isPhiResource: true);
        }

        instruction.Status = (TransitionInstructionStatus)(int)request.Status;
        if (instruction.Status == TransitionInstructionStatus.Modified)
        {
            instruction.InstructionText = request.ModifiedInstructionText!.Trim();
        }

        instruction.ClinicalNote = TrimToNull(request.ClinicalNote);
        instruction.NeedsPharmacistReview = request.NeedsPharmacistReview;

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TransitionInstructions.UpdateAsync(instruction, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await AuditAsync(ProtectedResourceType.TransitionInstruction, instruction.Id, AuditAction.Update, cancellationToken);
            await AuditAsync(ProtectedResourceType.TransitionInstruction, instruction.Id, AuditAction.Read, cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return instruction.ToClinicalDto();
    }

    public async Task<TransitionPlanClinicalDto> ActivatePlanAsync(
        Guid planId,
        ActivatePlanRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureClinician();
        await activatePlanValidator.ValidateAndThrowAsync(request, cancellationToken);
        var plan = await GetPlanAsync(planId, cancellationToken);
        if (plan.Status != TransitionPlanStatus.PendingVerification)
        {
            throw new ResourceConflictException("transition.plan_not_pending_verification", "transition.plan_not_pending_verification");
        }

        var instructions = await GetInstructionsAsync(plan.Id, cancellationToken);
        if (instructions.Count == 0 || instructions.Any(instruction => instruction.Status == TransitionInstructionStatus.Pending))
        {
            throw new ResourceConflictException("transition.instructions_pending_review", "transition.instructions_pending_review");
        }

        var now = DateTime.UtcNow;
        plan.Status = TransitionPlanStatus.Active;
        plan.RiskLevel = (TransitionRiskLevel)(int)request.RiskLevel;
        plan.VerifiedBy = currentUser.UserId ?? throw new ResourceAccessDeniedException("Unauthenticated", isPhiResource: true);
        plan.VerifiedAt = now;
        plan.ActivatedAt = now;
        plan.TransitionWindowEnd = DateTime.SpecifyKind(plan.DischargeDate, DateTimeKind.Utc).AddDays(30);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TransitionPlans.UpdateAsync(plan, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await AuditAsync(ProtectedResourceType.TransitionPlan, plan.Id, AuditAction.Update, cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        var client = await GetClientAsync(plan.ClientId, cancellationToken);
        var user = await GetUserAsync(client.UserId, cancellationToken);
        foreach (var instruction in instructions)
        {
            await AuditAsync(ProtectedResourceType.TransitionInstruction, instruction.Id, AuditAction.Read, cancellationToken);
        }

        return plan.ToClinicalDto(client, user, instructions);
    }

    public async Task<TransitionReminderDto> ScheduleReminderAsync(
        Guid planId,
        ScheduleReminderRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCoordinator();
        await scheduleReminderValidator.ValidateAndThrowAsync(request, cancellationToken);
        var plan = await GetPlanAsync(planId, cancellationToken);
        if (plan.Status != TransitionPlanStatus.Active)
        {
            throw new ResourceConflictException("transition.plan_not_active", "transition.plan_not_active");
        }

        if (request.ScheduledAt < plan.DischargeDate || request.ScheduledAt > plan.TransitionWindowEnd)
        {
            throw new ResourceConflictException("transition.outside_window", "transition.outside_window");
        }

        if (request.TransitionInstructionId.HasValue)
        {
            var instruction = await GetInstructionAsync(request.TransitionInstructionId.Value, cancellationToken);
            if (instruction.TransitionPlanId != plan.Id)
            {
                throw new ResourceNotFoundException(isPhiResource: true);
            }

            if (instruction.Status is not (TransitionInstructionStatus.Approved or TransitionInstructionStatus.Modified))
            {
                throw new ResourceConflictException("transition.instruction_not_schedulable", "transition.instruction_not_schedulable");
            }
        }

        var reminder = new TransitionReminder
        {
            TransitionPlanId = plan.Id,
            TransitionInstructionId = request.TransitionInstructionId,
            ReminderType = (ReminderType)(int)request.ReminderType,
            Channel = (ReminderChannel)(int)request.Channel,
            ScheduledAt = request.ScheduledAt,
            Status = ReminderStatus.Scheduled,
        };

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TransitionReminders.AddAsync(reminder, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await AuditAsync(ProtectedResourceType.TransitionReminder, reminder.Id, AuditAction.Create, cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return reminder.ToDto();
    }

    public async Task<TransitionCheckInDto> CreateCheckInAsync(
        Guid planId,
        CreateCheckInRequest request,
        CancellationToken cancellationToken = default)
    {
        await createCheckInValidator.ValidateAndThrowAsync(request, cancellationToken);
        var plan = await GetPlanAsync(planId, cancellationToken);
        EnsurePlanCanReceiveCheckIn(plan);
        await EnsureCanSubmitCheckInAsync(plan, cancellationToken);

        var checkIn = new TransitionCheckIn
        {
            TransitionPlanId = plan.Id,
            CheckInDate = DateTime.UtcNow,
            Channel = (ReminderChannel)(int)request.Channel,
            ResponsesJson = request.ResponsesJson,
            ContainsWarningSymptom = ContainsWarningSymptom(request.ResponsesJson),
        };
        TransitionEscalation? escalation = null;
        if (checkIn.ContainsWarningSymptom)
        {
            escalation = new TransitionEscalation
            {
                TransitionPlanId = plan.Id,
                TriggerType = EscalationTriggerType.WarningSymptomsReported,
                TriggerDetails = "Warning symptom check-in recorded.",
                EscalationLevel = EscalationLevel.CoordinatorAlert,
                EscalatedAt = DateTime.UtcNow,
            };
        }

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TransitionCheckIns.AddAsync(checkIn, cancellationToken);
            if (escalation is not null)
            {
                await unitOfWork.TransitionEscalations.AddAsync(escalation, cancellationToken);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await AuditAsync(ProtectedResourceType.TransitionCheckIn, checkIn.Id, AuditAction.Create, cancellationToken);
            if (escalation is not null)
            {
                await AuditAsync(ProtectedResourceType.TransitionEscalation, escalation.Id, AuditAction.Create, cancellationToken);
            }

            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return checkIn.ToDto();
    }

    public async Task<IReadOnlyList<TransitionEscalationDto>> GetEscalationsAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        EnsureCoordinator();
        _ = await GetPlanAsync(planId, cancellationToken);
        var escalations = await unitOfWork.TransitionEscalations.FindAsync(
            escalation => escalation.TransitionPlanId == planId,
            cancellationToken);
        foreach (var escalation in escalations)
        {
            await AuditAsync(ProtectedResourceType.TransitionEscalation, escalation.Id, AuditAction.Read, cancellationToken);
        }

        return escalations
            .OrderByDescending(escalation => escalation.EscalatedAt)
            .Select(escalation => escalation.ToDto())
            .ToArray();
    }

    public async Task<TransitionEscalationDto> AcknowledgeEscalationAsync(
        Guid escalationId,
        AcknowledgeEscalationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCoordinator();
        await acknowledgeEscalationValidator.ValidateAndThrowAsync(request, cancellationToken);
        var escalation = await GetEscalationAsync(escalationId, cancellationToken);
        escalation.EscalationLevel = (EscalationLevel)(int)request.EscalationLevel;
        escalation.ResolutionNote = request.ResolutionNote.Trim();
        escalation.AcknowledgedBy = currentUser.UserId ?? throw new ResourceAccessDeniedException("Unauthenticated", isPhiResource: true);
        escalation.AcknowledgedAt = DateTime.UtcNow;

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TransitionEscalations.UpdateAsync(escalation, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await AuditAsync(ProtectedResourceType.TransitionEscalation, escalation.Id, AuditAction.Update, cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return escalation.ToDto();
    }

    private async Task<DischargeDocument> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        return await unitOfWork.DischargeDocuments.GetByIdAsync(documentId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task<TransitionPlan> GetPlanAsync(Guid planId, CancellationToken cancellationToken)
    {
        return await unitOfWork.TransitionPlans.GetByIdAsync(planId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task<TransitionInstruction> GetInstructionAsync(Guid instructionId, CancellationToken cancellationToken)
    {
        return await unitOfWork.TransitionInstructions.GetByIdAsync(instructionId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task<TransitionEscalation> GetEscalationAsync(Guid escalationId, CancellationToken cancellationToken)
    {
        return await unitOfWork.TransitionEscalations.GetByIdAsync(escalationId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task<IReadOnlyList<TransitionInstruction>> GetInstructionsAsync(Guid planId, CancellationToken cancellationToken)
    {
        return await unitOfWork.TransitionInstructions.FindAsync(
            instruction => instruction.TransitionPlanId == planId,
            cancellationToken);
    }

    private async Task<Client> GetClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Clients.GetByIdAsync(clientId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task<User> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private void EnsureCoordinator()
    {
        if (!HasRole(ApplicationRoles.Coordinator))
        {
            throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
        }
    }

    private void EnsureClinician()
    {
        if (!HasRole(ApplicationRoles.Clinician))
        {
            throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
        }
    }

    private async Task EnsureCanSubmitCheckInAsync(TransitionPlan plan, CancellationToken cancellationToken)
    {
        if (!HasRole(ApplicationRoles.Client) || !currentUser.UserId.HasValue)
        {
            await AuditAsync(ProtectedResourceType.TransitionPlan, plan.Id, AuditAction.AccessDenied, cancellationToken);
            throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
        }

        var access = await clientAccessEvaluator.EvaluateAsync(
            currentUser.UserId.Value,
            plan.ClientId,
            AccessScope.PatientFacing,
            cancellationToken);
        if (!access.IsAuthorized)
        {
            await AuditAsync(ProtectedResourceType.TransitionPlan, plan.Id, AuditAction.AccessDenied, cancellationToken);
            throw new ResourceAccessDeniedException("NoGrant", isPhiResource: true);
        }
    }

    private static void EnsurePlanCanReceiveCheckIn(TransitionPlan plan)
    {
        if (plan.Status != TransitionPlanStatus.Active)
        {
            throw new ResourceConflictException("transition.plan_not_active", "transition.plan_not_active");
        }

        var now = DateTime.UtcNow;
        if (now < plan.DischargeDate || now > plan.TransitionWindowEnd)
        {
            throw new ResourceConflictException("transition.outside_window", "transition.outside_window");
        }
    }

    private void EnsureCoordinatorOrClinician()
    {
        if (!HasRole(ApplicationRoles.Coordinator) && !HasRole(ApplicationRoles.Clinician))
        {
            throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
        }
    }

    private bool HasRole(string role) => currentUser.Roles.Contains(role);

    private Task AuditAsync(
        ProtectedResourceType entityType,
        Guid entityId,
        AuditAction action,
        CancellationToken cancellationToken)
    {
        return auditLogger.LogAsync(
            new PhiAuditEntry(
                currentUser.UserId,
                currentUser.UserId.HasValue ? AuditActorType.User : AuditActorType.Anonymous,
                DateTime.UtcNow,
                action,
                entityType,
                entityId,
                currentUser.CorrelationId),
            cancellationToken);
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool ContainsWarningSymptom(string responsesJson)
    {
        var warningTerms = new[]
        {
            "chest pain",
            "shortness of breath",
            "difficulty breathing",
            "fever",
            "bleeding",
            "dizziness",
            "confusion",
            "fall"
        };

        return warningTerms.Any(term => responsesJson.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
