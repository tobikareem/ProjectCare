using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Common.Mapping;
using CarePath.Application.Common.Paging;
using CarePath.Application.Identity.Validators;
using CarePath.Contracts.Common;
using CarePath.Contracts.Identity;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentValidation;

namespace CarePath.Application.Identity.Services;

public sealed class CaregiverOperationsService : ICaregiverOperationsService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ICurrentUserContext currentUser;
    private readonly IIdentityProvisioningService identityProvisioning;
    private readonly IPhiAuditLogger auditLogger;
    private readonly IValidator<CreateCaregiverRequest> createValidator;
    private readonly IValidator<UpdateCaregiverRequest> updateValidator;
    private readonly IValidator<AddCertificationRequest> certificationValidator;

    public CaregiverOperationsService(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        IIdentityProvisioningService identityProvisioning,
        IPhiAuditLogger auditLogger,
        IValidator<CreateCaregiverRequest>? createValidator = null,
        IValidator<UpdateCaregiverRequest>? updateValidator = null,
        IValidator<AddCertificationRequest>? certificationValidator = null)
    {
        this.unitOfWork = unitOfWork;
        this.currentUser = currentUser;
        this.identityProvisioning = identityProvisioning;
        this.auditLogger = auditLogger;
        this.createValidator = createValidator ?? new CreateCaregiverRequestValidator();
        this.updateValidator = updateValidator ?? new UpdateCaregiverRequestValidator();
        this.certificationValidator = certificationValidator ?? new AddCertificationRequestValidator();
    }

    public async Task<CaregiverDetailDto> CreateCaregiverAsync(
        CreateCaregiverRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var existingUsers = await unitOfWork.Users.FindAsync(user => user.Email == request.Email, cancellationToken);
        var domainUser = existingUsers.SingleOrDefault();
        var isNewUser = domainUser is null;

        if (domainUser is not null)
        {
            if (domainUser.Role != UserRole.Caregiver || await unitOfWork.Caregivers.ExistsAsync(caregiver => caregiver.UserId == domainUser.Id, cancellationToken))
            {
                throw new ValidationException("The request is invalid.");
            }

            domainUser.PhoneNumber = request.PhoneNumber.Trim();
            domainUser.IsActive = true;
        }
        else
        {
            domainUser = new User
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = request.Email.Trim(),
                PhoneNumber = request.PhoneNumber.Trim(),
                Role = UserRole.Caregiver,
                IsActive = true,
            };
        }

        var caregiver = new Caregiver
        {
            UserId = domainUser.Id,
            User = domainUser,
            EmploymentType = (EmploymentType)(int)request.EmploymentType,
            HourlyPayRate = request.HourlyPayRate,
            HireDate = request.HireDate,
            HasDementiaCare = request.HasDementiaCare,
            HasAlzheimersCare = request.HasAlzheimersCare,
            HasMobilityAssistance = request.HasMobilityAssistance,
            HasMedicationManagement = request.HasMedicationManagement,
            AvailableWeekdays = request.AvailableWeekdays,
            AvailableWeekends = request.AvailableWeekends,
            AvailableNights = request.AvailableNights,
            MaxWeeklyHours = request.MaxWeeklyHours,
        };

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (isNewUser)
            {
                await unitOfWork.Users.AddAsync(domainUser, cancellationToken);
            }
            else
            {
                await unitOfWork.Users.UpdateAsync(domainUser, cancellationToken);
            }
            await unitOfWork.Caregivers.AddAsync(caregiver, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var provisioningResult = await identityProvisioning.ProvisionUserAsync(
                new IdentityProvisioningRequest(
                    domainUser.Id,
                    domainUser.Email,
                    domainUser.PhoneNumber,
                    request.TemporaryPassword!,
                    ApplicationRoles.Caregiver),
                cancellationToken);

            if (!provisioningResult.Succeeded)
            {
                throw new ValidationException("The account could not be provisioned.");
            }

            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return caregiver.ToDetailDto();
    }

    public async Task<CaregiverDetailDto> UpdateCaregiverAsync(
        Guid caregiverId,
        UpdateCaregiverRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var caregiver = await GetCaregiverEntityAsync(caregiverId, cancellationToken);
        var user = await GetUserAsync(caregiver.UserId, cancellationToken);

        user.PhoneNumber = request.PhoneNumber.Trim();
        user.IsActive = !request.TerminationDate.HasValue;
        caregiver.User = user;
        caregiver.HourlyPayRate = request.HourlyPayRate;
        caregiver.TerminationDate = request.TerminationDate;
        caregiver.HasDementiaCare = request.HasDementiaCare;
        caregiver.HasAlzheimersCare = request.HasAlzheimersCare;
        caregiver.HasMobilityAssistance = request.HasMobilityAssistance;
        caregiver.HasMedicationManagement = request.HasMedicationManagement;
        caregiver.AvailableWeekdays = request.AvailableWeekdays;
        caregiver.AvailableWeekends = request.AvailableWeekends;
        caregiver.AvailableNights = request.AvailableNights;
        caregiver.MaxWeeklyHours = request.MaxWeeklyHours;

        await unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await unitOfWork.Caregivers.UpdateAsync(caregiver, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        caregiver.Certifications = (await unitOfWork.CaregiverCertifications.FindAsync(
            certification => certification.CaregiverId == caregiver.Id,
            cancellationToken)).ToList();
        await AuditCertificationsAsync(caregiver.Certifications, cancellationToken);

        return caregiver.ToDetailDto();
    }

    public async Task<CaregiverDetailDto> GetCaregiverAsync(
        Guid caregiverId,
        CancellationToken cancellationToken = default)
    {
        var caregiver = await GetCaregiverEntityAsync(caregiverId, cancellationToken);
        if (!CanReadCaregiver(caregiver))
        {
            await AuditAsync(ProtectedResourceType.Caregiver, caregiver.Id, AuditAction.AccessDenied, cancellationToken);
            throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
        }

        caregiver.User = await GetUserAsync(caregiver.UserId, cancellationToken);
        caregiver.Certifications = (await unitOfWork.CaregiverCertifications.FindAsync(
            certification => certification.CaregiverId == caregiver.Id,
            cancellationToken)).ToList();
        await AuditCertificationsAsync(caregiver.Certifications, cancellationToken);

        return caregiver.ToDetailDto();
    }

    public async Task<PagedResult<CaregiverSummaryDto>> GetCaregiversAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!HasAnyRole(ApplicationRoles.Admin, ApplicationRoles.Coordinator))
        {
            return PagedResultFactory.Create(Array.Empty<CaregiverSummaryDto>(), 0, request.PageNumber, request.PageSize);
        }

        var (caregivers, totalCount) = await unitOfWork.Caregivers.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            cancellationToken);
        await AttachUsersAsync(caregivers, cancellationToken);

        return PagedResultFactory.Create(
            caregivers.Select(caregiver => caregiver.ToSummaryDto()).ToArray(),
            totalCount,
            request.PageNumber,
            request.PageSize);
    }

    public async Task<CertificationDto> AddCertificationAsync(
        Guid caregiverId,
        AddCertificationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        await certificationValidator.ValidateAndThrowAsync(request, cancellationToken);

        var caregiver = await GetCaregiverEntityAsync(caregiverId, cancellationToken);
        var certification = new CaregiverCertification
        {
            CaregiverId = caregiver.Id,
            Caregiver = caregiver,
            Type = (CertificationType)(int)request.Type,
            CertificationNumber = request.CertificationNumber,
            IssueDate = request.IssueDate,
            ExpirationDate = request.ExpirationDate,
            IssuingAuthority = request.IssuingAuthority,
        };

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.CaregiverCertifications.AddAsync(certification, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await AuditAsync(ProtectedResourceType.CaregiverCertification, certification.Id, AuditAction.Create, cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return certification.ToDto();
    }

    public async Task<PagedResult<CertificationDto>> GetExpiringCertificationsAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!HasAnyRole(ApplicationRoles.Admin, ApplicationRoles.Coordinator))
        {
            return PagedResultFactory.Create(Array.Empty<CertificationDto>(), 0, request.PageNumber, request.PageSize);
        }

        var now = DateTime.UtcNow;
        var threshold = now.AddDays(30);
        var certifications = await unitOfWork.CaregiverCertifications.FindAsync(
            certification => certification.ExpirationDate >= now && certification.ExpirationDate < threshold,
            cancellationToken);
        var ordered = certifications.OrderBy(certification => certification.ExpirationDate).ToArray();
        var page = PagedResultFactory.Page(ordered, request.PageNumber, request.PageSize);

        foreach (var certification in page)
        {
            await AuditAsync(ProtectedResourceType.CaregiverCertification, certification.Id, AuditAction.Read, cancellationToken);
        }

        return PagedResultFactory.Create(
            page.Select(certification => certification.ToDto()).ToArray(),
            ordered.Length,
            request.PageNumber,
            request.PageSize);
    }


    private async Task AuditCertificationsAsync(
        IEnumerable<CaregiverCertification> certifications,
        CancellationToken cancellationToken)
    {
        foreach (var certification in certifications)
        {
            await AuditAsync(ProtectedResourceType.CaregiverCertification, certification.Id, AuditAction.Read, cancellationToken);
        }
    }
    private async Task<Caregiver> GetCaregiverEntityAsync(Guid caregiverId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Caregivers.GetByIdAsync(caregiverId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task<User> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task AttachUsersAsync(IReadOnlyList<Caregiver> caregivers, CancellationToken cancellationToken)
    {
        foreach (var caregiver in caregivers)
        {
            caregiver.User = await GetUserAsync(caregiver.UserId, cancellationToken);
        }
    }

    private bool CanReadCaregiver(Caregiver caregiver)
    {
        return HasAnyRole(ApplicationRoles.Admin, ApplicationRoles.Coordinator)
            || (HasRole(ApplicationRoles.Caregiver) && currentUser.UserId == caregiver.UserId);
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
}



