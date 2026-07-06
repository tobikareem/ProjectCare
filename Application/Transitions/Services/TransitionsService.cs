using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Common.Mapping;
using CarePath.Application.Transitions.Interfaces;
using CarePath.Application.Transitions.Validators;
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

    public TransitionsService(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        IPhiAuditLogger auditLogger,
        IDischargeExtractionService extractionService,
        IValidator<CreateDischargeDocumentRequest>? createDocumentValidator = null)
    {
        this.unitOfWork = unitOfWork;
        this.currentUser = currentUser;
        this.auditLogger = auditLogger;
        this.extractionService = extractionService;
        this.createDocumentValidator = createDocumentValidator ?? new CreateDischargeDocumentRequestValidator();
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
            return plan.ToClinicalDto(client, user, instructions);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task<DischargeDocument> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        return await unitOfWork.DischargeDocuments.GetByIdAsync(documentId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
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
}
