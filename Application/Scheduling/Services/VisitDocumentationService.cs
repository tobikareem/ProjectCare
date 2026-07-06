using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Storage;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Common.Mapping;
using CarePath.Application.Common.Paging;
using CarePath.Contracts.Common;
using CarePath.Contracts.Scheduling;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentValidation;

namespace CarePath.Application.Scheduling.Services;

public sealed class VisitDocumentationService : IVisitDocumentationService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ICurrentUserContext currentUser;
    private readonly IClientAccessEvaluator clientAccessEvaluator;
    private readonly IPhiAuditLogger auditLogger;
    private readonly IFileStorageService fileStorage;
    private readonly IValidator<CreateVisitNoteRequest> noteValidator;

    public VisitDocumentationService(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        IClientAccessEvaluator clientAccessEvaluator,
        IPhiAuditLogger auditLogger,
        IFileStorageService fileStorage,
        IValidator<CreateVisitNoteRequest>? noteValidator = null)
    {
        this.unitOfWork = unitOfWork;
        this.currentUser = currentUser;
        this.clientAccessEvaluator = clientAccessEvaluator;
        this.auditLogger = auditLogger;
        this.fileStorage = fileStorage;
        this.noteValidator = noteValidator ?? new Validators.CreateVisitNoteRequestValidator();
    }

    public async Task<VisitNoteDetailDto> CreateVisitNoteAsync(Guid shiftId, CreateVisitNoteRequest request, CancellationToken cancellationToken = default)
    {
        await noteValidator.ValidateAndThrowAsync(request, cancellationToken);
        var shift = await GetShiftAsync(shiftId, cancellationToken);
        await EnsureAssignedCaregiverAsync(shift, cancellationToken);

        var note = new VisitNote
        {
            ShiftId = shift.Id,
            Shift = shift,
            CaregiverId = shift.CaregiverId!.Value,
            VisitDateTime = request.VisitDateTime,
            PersonalCare = request.PersonalCare,
            MealPreparation = request.MealPreparation,
            Medication = request.Medication,
            LightHousekeeping = request.LightHousekeeping,
            Companionship = request.Companionship,
            Transportation = request.Transportation,
            Exercise = request.Exercise,
            Activities = request.Activities,
            ClientCondition = request.ClientCondition,
            Concerns = request.Concerns,
            Medications = request.Medications,
            BloodPressureSystolic = request.BloodPressureSystolic,
            BloodPressureDiastolic = request.BloodPressureDiastolic,
            Temperature = request.Temperature,
            HeartRate = request.HeartRate,
        };

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.VisitNotes.AddAsync(note, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await AuditAsync(ProtectedResourceType.VisitNote, note.Id, AuditAction.Create, cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return note.ToDetailDto();
    }

    public async Task<PagedResult<VisitNoteSummaryDto>> GetVisitNotesAsync(Guid shiftId, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var shift = await GetShiftAsync(shiftId, cancellationToken);
        if (!await CanReadShiftAsync(shift, cancellationToken))
        {
            return PagedResultFactory.Create(Array.Empty<VisitNoteSummaryDto>(), 0, request.PageNumber, request.PageSize);
        }

        var (notes, totalCount) = await unitOfWork.VisitNotes.GetPagedAsync(
            note => note.ShiftId == shift.Id,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        foreach (var note in notes)
        {
            await AuditAsync(ProtectedResourceType.VisitNote, note.Id, AuditAction.Read, cancellationToken);
        }

        return PagedResultFactory.Create(notes.Select(note => note.ToSummaryDto()).ToArray(), totalCount, request.PageNumber, request.PageSize);
    }

    public async Task<VisitNoteDetailDto> GetVisitNoteAsync(Guid visitNoteId, CancellationToken cancellationToken = default)
    {
        var note = await GetVisitNoteEntityAsync(visitNoteId, cancellationToken);
        var shift = await GetShiftAsync(note.ShiftId, cancellationToken);
        await EnsureCanReadShiftAsync(shift, cancellationToken);
        note.Photos = (await unitOfWork.VisitPhotos.FindAsync(photo => photo.VisitNoteId == note.Id, cancellationToken)).ToList();
        foreach (var photo in note.Photos)
        {
            await AuditAsync(ProtectedResourceType.VisitPhoto, photo.Id, AuditAction.Read, cancellationToken);
        }

        await AuditAsync(ProtectedResourceType.VisitNote, note.Id, AuditAction.Read, cancellationToken);
        return note.ToDetailDto();
    }

    public async Task<VisitPhotoDto> AddVisitPhotoAsync(
        Guid visitNoteId,
        string fileName,
        string contentType,
        Stream content,
        string? caption,
        DateTime takenAtUtc,
        CancellationToken cancellationToken = default)
    {
        var note = await GetVisitNoteEntityAsync(visitNoteId, cancellationToken);
        var shift = await GetShiftAsync(note.ShiftId, cancellationToken);
        await EnsureAssignedCaregiverAsync(shift, cancellationToken);

        if (takenAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ValidationException("Photo timestamp must be UTC.");
        }

        var objectId = await fileStorage.SaveAsync(new FileStorageWriteRequest(fileName, contentType, content), cancellationToken);
        var committed = false;
        var photo = new VisitPhoto
        {
            VisitNoteId = note.Id,
            VisitNote = note,
            PhotoUrl = objectId,
            Caption = caption,
            TakenAt = takenAtUtc,
        };

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.VisitPhotos.AddAsync(photo, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await AuditAsync(ProtectedResourceType.VisitPhoto, photo.Id, AuditAction.Create, cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            committed = true;
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (!committed)
            {
                await fileStorage.DeleteAsync(objectId, cancellationToken);
            }
        }

        return photo.ToDto();
    }

    private async Task EnsureAssignedCaregiverAsync(Shift shift, CancellationToken cancellationToken)
    {
        if (!HasRole(ApplicationRoles.Caregiver) || !await IsCurrentCaregiverAsync(shift.CaregiverId, cancellationToken))
        {
            await AuditAsync(ProtectedResourceType.Shift, shift.Id, AuditAction.AccessDenied, cancellationToken);
            throw new ResourceAccessDeniedException("NotAssigned", isPhiResource: true);
        }
    }

    private async Task EnsureCanReadShiftAsync(Shift shift, CancellationToken cancellationToken)
    {
        if (await CanReadShiftAsync(shift, cancellationToken))
        {
            return;
        }

        await AuditAsync(ProtectedResourceType.Shift, shift.Id, AuditAction.AccessDenied, cancellationToken);
        throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
    }

    private async Task<bool> CanReadShiftAsync(Shift shift, CancellationToken cancellationToken)
    {
        if (HasAnyRole(ApplicationRoles.Admin, ApplicationRoles.Coordinator))
        {
            return true;
        }

        if (HasRole(ApplicationRoles.Caregiver) && await IsCurrentCaregiverAsync(shift.CaregiverId, cancellationToken))
        {
            return true;
        }

        if (HasRole(ApplicationRoles.Client) && currentUser.UserId.HasValue)
        {
            var result = await clientAccessEvaluator.EvaluateAsync(currentUser.UserId.Value, shift.ClientId, AccessScope.Full, cancellationToken);
            return result.IsAuthorized;
        }

        return false;
    }

    private async Task<bool> IsCurrentCaregiverAsync(Guid? caregiverId, CancellationToken cancellationToken)
    {
        if (!caregiverId.HasValue || !currentUser.UserId.HasValue)
        {
            return false;
        }

        var caregivers = await unitOfWork.Caregivers.FindAsync(caregiver => caregiver.UserId == currentUser.UserId.Value, cancellationToken);
        return caregivers.Any(caregiver => caregiver.Id == caregiverId.Value);
    }

    private async Task<Shift> GetShiftAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Shifts.GetByIdAsync(shiftId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task<VisitNote> GetVisitNoteEntityAsync(Guid visitNoteId, CancellationToken cancellationToken)
    {
        return await unitOfWork.VisitNotes.GetByIdAsync(visitNoteId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private bool HasAnyRole(params string[] roles) => roles.Any(HasRole);

    private bool HasRole(string role) => currentUser.Roles.Contains(role);

    private Task AuditAsync(ProtectedResourceType entityType, Guid entityId, AuditAction action, CancellationToken cancellationToken)
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
}


