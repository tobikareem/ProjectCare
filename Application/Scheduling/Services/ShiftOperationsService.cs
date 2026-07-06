using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Common.Mapping;
using CarePath.Application.Common.Paging;
using CarePath.Application.Scheduling.Validators;
using CarePath.Contracts.Common;
using CarePath.Contracts.Scheduling;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentValidation;
using FluentValidation.Results;

namespace CarePath.Application.Scheduling.Services;

public sealed class ShiftOperationsService : IShiftOperationsService
{
    private const string DoubleBookedCode = "shift.double_booked";
    private const string CertificationExpiredCode = "caregiver.certification_expired";
    private const string InvalidLifecycleCode = "shift.invalid_lifecycle";

    private readonly IUnitOfWork unitOfWork;
    private readonly ICurrentUserContext currentUser;
    private readonly IClientAccessEvaluator clientAccessEvaluator;
    private readonly IPhiAuditLogger auditLogger;
    private readonly IValidator<CreateShiftRequest> createValidator;
    private readonly IValidator<UpdateShiftRequest> updateValidator;
    private readonly IValidator<CheckInRequest> checkInValidator;
    private readonly IValidator<CheckOutRequest> checkOutValidator;

    public ShiftOperationsService(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        IClientAccessEvaluator clientAccessEvaluator,
        IPhiAuditLogger auditLogger,
        IValidator<CreateShiftRequest>? createValidator = null,
        IValidator<UpdateShiftRequest>? updateValidator = null,
        IValidator<CheckInRequest>? checkInValidator = null,
        IValidator<CheckOutRequest>? checkOutValidator = null)
    {
        this.unitOfWork = unitOfWork;
        this.currentUser = currentUser;
        this.clientAccessEvaluator = clientAccessEvaluator;
        this.auditLogger = auditLogger;
        this.createValidator = createValidator ?? new CreateShiftRequestValidator();
        this.updateValidator = updateValidator ?? new UpdateShiftRequestValidator();
        this.checkInValidator = checkInValidator ?? new CheckInRequestValidator();
        this.checkOutValidator = checkOutValidator ?? new CheckOutRequestValidator();
    }

    public async Task<ShiftDetailDto> CreateShiftAsync(CreateShiftRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var client = await GetClientAsync(request.ClientId, cancellationToken);
        var caregiver = await GetCaregiverAsync(request.CaregiverId, cancellationToken);
        await EnsureCaregiverCanBeAssignedAsync(caregiver.Id, request.ScheduledStartUtc, cancellationToken);
        await EnsureCaregiverIsNotDoubleBookedAsync(caregiver.Id, request.ScheduledStartUtc, request.ScheduledEndUtc, excludingShiftId: null, cancellationToken);

        var shift = new Shift
        {
            ClientId = client.Id,
            Client = client,
            CaregiverId = caregiver.Id,
            Caregiver = caregiver,
            ScheduledStartTime = request.ScheduledStartUtc,
            ScheduledEndTime = request.ScheduledEndUtc,
            BreakMinutes = request.BreakMinutes ?? 0,
            BillRate = request.BillRate,
            PayRate = request.PayRate,
            ServiceType = (ServiceType)(int)request.ServiceType,
            Status = ShiftStatus.Scheduled,
        };

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.Shifts.AddAsync(shift, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await AuditAsync(shift.Id, AuditAction.Create, cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        await AttachShiftUsersAsync(shift, cancellationToken);
        return shift.ToDetailDto();
    }

    public async Task<ShiftDetailDto> UpdateShiftAsync(Guid shiftId, UpdateShiftRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var shift = await GetShiftEntityAsync(shiftId, cancellationToken);
        var caregiverId = request.CaregiverId;
        if (caregiverId.HasValue)
        {
            _ = await GetCaregiverAsync(caregiverId.Value, cancellationToken);
            await EnsureCaregiverCanBeAssignedAsync(caregiverId.Value, request.ScheduledStartUtc, cancellationToken);
            await EnsureCaregiverIsNotDoubleBookedAsync(caregiverId.Value, request.ScheduledStartUtc, request.ScheduledEndUtc, shift.Id, cancellationToken);
        }

        shift.CaregiverId = caregiverId;
        shift.ScheduledStartTime = request.ScheduledStartUtc;
        shift.ScheduledEndTime = request.ScheduledEndUtc;
        shift.BreakMinutes = request.BreakMinutes ?? 0;
        shift.BillRate = request.BillRate;
        shift.PayRate = request.PayRate;
        shift.ServiceType = (ServiceType)(int)request.ServiceType;
        shift.Notes = request.Notes;

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.Shifts.UpdateAsync(shift, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await AuditAsync(shift.Id, AuditAction.Update, cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        await AttachShiftUsersAsync(shift, cancellationToken);
        return shift.ToDetailDto();
    }

    public async Task CancelShiftAsync(Guid shiftId, string cancellationReason, CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        var shift = await GetShiftEntityAsync(shiftId, cancellationToken);
        shift.Status = ShiftStatus.Cancelled;
        shift.CancelledAt = DateTime.UtcNow;
        shift.CancellationReason = string.IsNullOrWhiteSpace(cancellationReason) ? "Cancelled by coordinator." : cancellationReason.Trim();

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.Shifts.UpdateAsync(shift, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await AuditAsync(shift.Id, AuditAction.Update, cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ShiftDetailDto> CheckInAsync(CheckInRequest request, CancellationToken cancellationToken = default)
    {
        await checkInValidator.ValidateAndThrowAsync(request, cancellationToken);
        var shift = await GetShiftEntityAsync(request.ShiftId, cancellationToken);
        await EnsureCurrentCaregiverOwnsShiftAsync(shift, cancellationToken);

        if (shift.Status != ShiftStatus.Scheduled || shift.CheckInTime.HasValue || shift.ActualStartTime.HasValue)
        {
            throw ValidationFailure("ShiftId", "The shift cannot be checked in from its current status.", InvalidLifecycleCode);
        }

        var now = DateTime.UtcNow;
        shift.CheckInLatitude = request.Latitude;
        shift.CheckInLongitude = request.Longitude;
        shift.CheckInTime = now;
        shift.ActualStartTime = now;
        shift.Status = ShiftStatus.InProgress;

        await SaveShiftWithAuditAsync(shift, AuditAction.Update, cancellationToken);
        await AttachShiftUsersAsync(shift, cancellationToken);
        return shift.ToDetailDto();
    }

    public async Task<ShiftDetailDto> CheckOutAsync(CheckOutRequest request, CancellationToken cancellationToken = default)
    {
        await checkOutValidator.ValidateAndThrowAsync(request, cancellationToken);
        var shift = await GetShiftEntityAsync(request.ShiftId, cancellationToken);
        await EnsureCurrentCaregiverOwnsShiftAsync(shift, cancellationToken);

        if (shift.Status != ShiftStatus.InProgress || !shift.ActualStartTime.HasValue || shift.CheckOutTime.HasValue)
        {
            throw ValidationFailure("ShiftId", "The shift cannot be checked out from its current status.", InvalidLifecycleCode);
        }

        var now = DateTime.UtcNow;
        shift.CheckOutLatitude = request.Latitude;
        shift.CheckOutLongitude = request.Longitude;
        shift.CheckOutTime = now;
        shift.ActualEndTime = now;
        shift.BreakMinutes = request.BreakMinutes ?? shift.BreakMinutes;
        shift.Status = ShiftStatus.Completed;

        await SaveShiftWithAuditAsync(shift, AuditAction.Update, cancellationToken);
        await AttachShiftUsersAsync(shift, cancellationToken);
        return shift.ToDetailDto();
    }

    public async Task<ShiftDetailDto> GetShiftAsync(Guid shiftId, CancellationToken cancellationToken = default)
    {
        var shift = await GetShiftEntityAsync(shiftId, cancellationToken);
        await EnsureCanReadShiftAsync(shift, cancellationToken);
        await AttachShiftUsersAsync(shift, cancellationToken);
        await AuditAsync(shift.Id, AuditAction.Read, cancellationToken);
        return shift.ToDetailDto();
    }

    public async Task<PagedResult<ShiftSummaryDto>> GetShiftsAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Shift> shifts;
        int totalCount;

        if (HasAnyRole(ApplicationRoles.Admin, ApplicationRoles.Coordinator))
        {
            (shifts, totalCount) = await unitOfWork.Shifts.GetPagedAsync(request.PageNumber, request.PageSize, cancellationToken);
        }
        else
        {
            (shifts, totalCount) = await GetScopedShiftPageAsync(request, cancellationToken);
        }

        foreach (var shift in shifts)
        {
            await AttachShiftUsersAsync(shift, cancellationToken);
            await AuditAsync(shift.Id, AuditAction.Read, cancellationToken);
        }

        return PagedResultFactory.Create(
            shifts.Select(shift => shift.ToSummaryDto()).ToArray(),
            totalCount,
            request.PageNumber,
            request.PageSize);
    }

    private async Task SaveShiftWithAuditAsync(Shift shift, AuditAction action, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.Shifts.UpdateAsync(shift, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await AuditAsync(shift.Id, action, cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task EnsureCaregiverIsNotDoubleBookedAsync(
        Guid caregiverId,
        DateTime startUtc,
        DateTime endUtc,
        Guid? excludingShiftId,
        CancellationToken cancellationToken)
    {
        var overlapping = await unitOfWork.Shifts.ExistsAsync(
            shift => shift.CaregiverId == caregiverId
                && shift.Status != ShiftStatus.Cancelled
                && shift.Status != ShiftStatus.Completed
                && (!excludingShiftId.HasValue || shift.Id != excludingShiftId.Value)
                && shift.ScheduledStartTime < endUtc
                && startUtc < shift.ScheduledEndTime,
            cancellationToken);

        if (overlapping)
        {
            throw ValidationFailure("Schedule", "The caregiver is unavailable for the requested shift window.", DoubleBookedCode);
        }
    }

    private async Task EnsureCaregiverCanBeAssignedAsync(
        Guid caregiverId,
        DateTime scheduledStartUtc,
        CancellationToken cancellationToken)
    {
        var certifications = await unitOfWork.CaregiverCertifications.FindAsync(
            certification => certification.CaregiverId == caregiverId,
            cancellationToken);
        foreach (var certification in certifications)
        {
            await auditLogger.LogAsync(
                new PhiAuditEntry(
                    currentUser.UserId,
                    currentUser.UserId.HasValue ? AuditActorType.User : AuditActorType.Anonymous,
                    DateTime.UtcNow,
                    AuditAction.Read,
                    ProtectedResourceType.CaregiverCertification,
                    certification.Id,
                    currentUser.CorrelationId),
                cancellationToken);
        }

        var requiredTypes = GetRequiredCertificationTypes().ToArray();
        var hasValidRequiredCertification = certifications.Any(certification =>
            requiredTypes.Contains(certification.Type)
            && certification.ExpirationDate.Date >= scheduledStartUtc.Date);

        if (!hasValidRequiredCertification)
        {
            throw ValidationFailure("CaregiverId", "The caregiver is not eligible for the requested shift.", CertificationExpiredCode);
        }
    }

    private static IReadOnlyList<CertificationType> GetRequiredCertificationTypes()
    {
        return new[]
        {
            CertificationType.HHA,
            CertificationType.CNA,
            CertificationType.GNA,
            CertificationType.LPN,
            CertificationType.RN,
        };
    }

    private async Task EnsureCanReadShiftAsync(Shift shift, CancellationToken cancellationToken)
    {
        if (HasAnyRole(ApplicationRoles.Admin, ApplicationRoles.Coordinator))
        {
            return;
        }

        if (HasRole(ApplicationRoles.Caregiver) && await IsCurrentCaregiverAsync(shift.CaregiverId, cancellationToken))
        {
            return;
        }

        if (HasRole(ApplicationRoles.Client) && currentUser.UserId.HasValue)
        {
            var result = await clientAccessEvaluator.EvaluateAsync(currentUser.UserId.Value, shift.ClientId, AccessScope.Full, cancellationToken);
            if (result.IsAuthorized)
            {
                return;
            }
        }

        await AuditAsync(shift.Id, AuditAction.AccessDenied, cancellationToken);
        throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
    }

    private async Task EnsureCurrentCaregiverOwnsShiftAsync(Shift shift, CancellationToken cancellationToken)
    {
        if (!HasRole(ApplicationRoles.Caregiver) || !await IsCurrentCaregiverAsync(shift.CaregiverId, cancellationToken))
        {
            await AuditAsync(shift.Id, AuditAction.AccessDenied, cancellationToken);
            throw new ResourceAccessDeniedException("NotAssigned", isPhiResource: true);
        }
    }

    private async Task<bool> IsCurrentCaregiverAsync(Guid? caregiverId, CancellationToken cancellationToken)
    {
        if (!caregiverId.HasValue || !currentUser.UserId.HasValue)
        {
            return false;
        }

        var caregivers = await unitOfWork.Caregivers.FindAsync(
            caregiver => caregiver.UserId == currentUser.UserId.Value,
            cancellationToken);
        return caregivers.Any(caregiver => caregiver.Id == caregiverId.Value);
    }

    private async Task<(IReadOnlyList<Shift> Items, int TotalCount)> GetScopedShiftPageAsync(
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return (Array.Empty<Shift>(), 0);
        }

        if (HasRole(ApplicationRoles.Caregiver))
        {
            var caregivers = await unitOfWork.Caregivers.FindAsync(
                caregiver => caregiver.UserId == currentUser.UserId.Value,
                cancellationToken);
            var caregiverIds = caregivers.Select(caregiver => caregiver.Id).ToArray();
            return caregiverIds.Length == 0
                ? (Array.Empty<Shift>(), 0)
                : await unitOfWork.Shifts.GetPagedAsync(
                    shift => shift.CaregiverId.HasValue && caregiverIds.Contains(shift.CaregiverId.Value),
                    request.PageNumber,
                    request.PageSize,
                    cancellationToken);
        }

        if (HasRole(ApplicationRoles.Client))
        {
            var clients = await unitOfWork.Clients.FindAsync(client => client.UserId == currentUser.UserId.Value, cancellationToken);
            var grants = await unitOfWork.ClientAccessGrants.FindAsync(
                grant => grant.GranteeUserId == currentUser.UserId.Value
                    && grant.RevokedAtUtc == null
                    && (grant.AccessScope == AccessScope.PatientFacing || grant.AccessScope == AccessScope.Full),
                cancellationToken);
            foreach (var grant in grants)
            {
                await auditLogger.LogAsync(
                    new PhiAuditEntry(
                        currentUser.UserId,
                        currentUser.UserId.HasValue ? AuditActorType.User : AuditActorType.Anonymous,
                        DateTime.UtcNow,
                        AuditAction.Read,
                        ProtectedResourceType.ClientAccessGrant,
                        grant.Id,
                        currentUser.CorrelationId),
                    cancellationToken);
            }

            var clientIds = clients.Select(client => client.Id).Concat(grants.Select(grant => grant.ClientId)).Distinct().ToArray();
            return clientIds.Length == 0
                ? (Array.Empty<Shift>(), 0)
                : await unitOfWork.Shifts.GetPagedAsync(
                    shift => clientIds.Contains(shift.ClientId),
                    request.PageNumber,
                    request.PageSize,
                    cancellationToken);
        }

        return (Array.Empty<Shift>(), 0);
    }

    private async Task AttachShiftUsersAsync(Shift shift, CancellationToken cancellationToken)
    {
        shift.Client = await GetClientAsync(shift.ClientId, cancellationToken);
        shift.Client.User = await GetUserAsync(shift.Client.UserId, cancellationToken);
        if (shift.CaregiverId.HasValue)
        {
            shift.Caregiver = await GetCaregiverAsync(shift.CaregiverId.Value, cancellationToken);
            shift.Caregiver.User = await GetUserAsync(shift.Caregiver.UserId, cancellationToken);
        }
    }

    private async Task<Client> GetClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Clients.GetByIdAsync(clientId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task<Caregiver> GetCaregiverAsync(Guid caregiverId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Caregivers.GetByIdAsync(caregiverId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task<User> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task<Shift> GetShiftEntityAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Shifts.GetByIdAsync(shiftId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private void EnsureAdminOrCoordinator()
    {
        if (!HasAnyRole(ApplicationRoles.Admin, ApplicationRoles.Coordinator))
        {
            throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
        }
    }

    private bool HasAnyRole(params string[] roles) => roles.Any(HasRole);

    private bool HasRole(string role) => currentUser.Roles.Contains(role);

    private Task AuditAsync(Guid shiftId, AuditAction action, CancellationToken cancellationToken)
    {
        return auditLogger.LogAsync(
            new PhiAuditEntry(
                currentUser.UserId,
                currentUser.UserId.HasValue ? AuditActorType.User : AuditActorType.Anonymous,
                DateTime.UtcNow,
                action,
                ProtectedResourceType.Shift,
                shiftId,
                currentUser.CorrelationId),
            cancellationToken);
    }

    private static ValidationException ValidationFailure(string propertyName, string message, string code)
    {
        return new ValidationException(new[] { new ValidationFailure(propertyName, message) { ErrorCode = code } });
    }
}



