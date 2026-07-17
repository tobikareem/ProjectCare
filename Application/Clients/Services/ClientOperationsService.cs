using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Clients.Validators;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Common.Mapping;
using CarePath.Application.Common.Paging;
using CarePath.Contracts.Clients;
using CarePath.Contracts.Common;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentValidation;

namespace CarePath.Application.Clients.Services;

public sealed class ClientOperationsService : IClientOperationsService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ICurrentUserContext currentUser;
    private readonly IIdentityProvisioningService identityProvisioning;
    private readonly IClientAccessEvaluator clientAccessEvaluator;
    private readonly IPhiAuditLogger auditLogger;
    private readonly IValidator<CreateClientRequest> createClientValidator;
    private readonly IValidator<UpdateClientRequest> updateClientValidator;
    private readonly IValidator<CreateCarePlanRequest> createCarePlanValidator;
    private readonly IValidator<UpdateCarePlanRequest> updateCarePlanValidator;

    public ClientOperationsService(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        IIdentityProvisioningService identityProvisioning,
        IClientAccessEvaluator clientAccessEvaluator,
        IPhiAuditLogger auditLogger,
        IValidator<CreateClientRequest>? createClientValidator = null,
        IValidator<UpdateClientRequest>? updateClientValidator = null,
        IValidator<CreateCarePlanRequest>? createCarePlanValidator = null,
        IValidator<UpdateCarePlanRequest>? updateCarePlanValidator = null)
    {
        this.unitOfWork = unitOfWork;
        this.currentUser = currentUser;
        this.identityProvisioning = identityProvisioning;
        this.clientAccessEvaluator = clientAccessEvaluator;
        this.auditLogger = auditLogger;
        this.createClientValidator = createClientValidator ?? new CreateClientRequestValidator();
        this.updateClientValidator = updateClientValidator ?? new UpdateClientRequestValidator();
        this.createCarePlanValidator = createCarePlanValidator ?? new CreateCarePlanRequestValidator();
        this.updateCarePlanValidator = updateCarePlanValidator ?? new UpdateCarePlanRequestValidator();
    }

    public async Task<ClientDetailDto> CreateClientAsync(
        CreateClientRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        await createClientValidator.ValidateAndThrowAsync(request, cancellationToken);

        var existingUsers = await unitOfWork.Users.FindAsync(user => user.Email == request.Email, cancellationToken);
        var domainUser = existingUsers.SingleOrDefault();
        var isNewUser = domainUser is null;

        if (domainUser is not null)
        {
            if (domainUser.Role != UserRole.Client || await unitOfWork.Clients.ExistsAsync(client => client.UserId == domainUser.Id, cancellationToken))
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
                Role = UserRole.Client,
                IsActive = true,
            };
        }

        var client = new Client
        {
            UserId = domainUser.Id,
            User = domainUser,
            DateOfBirth = request.DateOfBirth,
            EmergencyContactName = request.EmergencyContactName,
            EmergencyContactPhone = request.EmergencyContactPhone,
            EmergencyContactRelationship = request.EmergencyContactRelationship,
            RequiresDementiaCare = request.RequiresDementiaCare,
            RequiresMobilityAssistance = request.RequiresMobilityAssistance,
            RequiresMedicationManagement = request.RequiresMedicationManagement,
            RequiresCompanionship = request.RequiresCompanionship,
            SpecialInstructions = request.SpecialInstructions,
            MedicalConditions = request.MedicalConditions,
            Allergies = request.Allergies,
            ServiceType = (ServiceType)(int)request.ServiceType,
            HourlyBillRate = request.HourlyBillRate,
            EstimatedWeeklyHours = request.EstimatedWeeklyHours,
        };

        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                if (isNewUser)
                {
                    await unitOfWork.Users.AddAsync(domainUser, token);
                }
                else
                {
                    await unitOfWork.Users.UpdateAsync(domainUser, token);
                }
                await unitOfWork.Clients.AddAsync(client, token);
                await unitOfWork.SaveChangesAsync(token);

                var provisioningResult = await identityProvisioning.ProvisionUserAsync(
                    new IdentityProvisioningRequest(
                        domainUser.Id,
                        domainUser.Email,
                        domainUser.PhoneNumber,
                        request.TemporaryPassword!,
                        ApplicationRoles.Client),
                    token);

                if (!provisioningResult.Succeeded)
                {
                    throw new ValidationException("The account could not be provisioned.");
                }

                await AuditAsync(ProtectedResourceType.Client, client.Id, AuditAction.Create, token);
            },
            cancellationToken);

        return client.ToDetailDto(includeOperationalFields: HasOperationalClientDetailAccess());
    }

    public async Task<ClientDetailDto> UpdateClientAsync(
        Guid clientId,
        UpdateClientRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        await updateClientValidator.ValidateAndThrowAsync(request, cancellationToken);

        var client = await GetClientEntityAsync(clientId, cancellationToken);
        var user = await GetUserAsync(client.UserId, cancellationToken);
        client.User = user;

        user.PhoneNumber = request.PhoneNumber.Trim();
        client.EmergencyContactName = request.EmergencyContactName;
        client.EmergencyContactPhone = request.EmergencyContactPhone;
        client.EmergencyContactRelationship = request.EmergencyContactRelationship;
        client.RequiresDementiaCare = request.RequiresDementiaCare;
        client.RequiresMobilityAssistance = request.RequiresMobilityAssistance;
        client.RequiresMedicationManagement = request.RequiresMedicationManagement;
        client.RequiresCompanionship = request.RequiresCompanionship;
        client.SpecialInstructions = request.SpecialInstructions;
        client.MedicalConditions = request.MedicalConditions;
        client.Allergies = request.Allergies;
        client.ServiceType = (ServiceType)(int)request.ServiceType;
        client.HourlyBillRate = request.HourlyBillRate;
        client.EstimatedWeeklyHours = request.EstimatedWeeklyHours;

        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                await unitOfWork.Users.UpdateAsync(user, token);
                await unitOfWork.Clients.UpdateAsync(client, token);
                await unitOfWork.SaveChangesAsync(token);
                await AuditAsync(ProtectedResourceType.Client, client.Id, AuditAction.Update, token);
            },
            cancellationToken);

        return client.ToDetailDto(includeOperationalFields: HasOperationalClientDetailAccess());
    }

    public async Task<ClientDetailDto?> GetClientAsync(
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        var client = await unitOfWork.Clients.GetByIdAsync(clientId, cancellationToken);
        if (client is null)
        {
            return null;
        }

        await EnsureCanReadClientAsync(client.Id, AccessScope.Full, cancellationToken);

        client.User = await GetUserAsync(client.UserId, cancellationToken);
        await AuditAsync(ProtectedResourceType.Client, client.Id, AuditAction.Read, cancellationToken);

        return client.ToDetailDto(includeOperationalFields: HasOperationalClientDetailAccess());
    }

    public async Task<PagedResult<ClientSummaryDto>> GetClientsAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Client> clients;
        int totalCount;

        if (HasAnyRole(ApplicationRoles.Admin, ApplicationRoles.Coordinator))
        {
            (clients, totalCount) = await unitOfWork.Clients.GetPagedAsync(
                request.PageNumber,
                request.PageSize,
                cancellationToken);
        }
        else
        {
            var scopedClients = await GetScopedClientsAsync(cancellationToken);
            totalCount = scopedClients.Count;
            clients = PagedResultFactory.Page(scopedClients, request.PageNumber, request.PageSize);
        }

        await AttachUsersAsync(clients, cancellationToken);
        foreach (var client in clients)
        {
            await AuditAsync(ProtectedResourceType.Client, client.Id, AuditAction.Read, cancellationToken);
        }

        return PagedResultFactory.Create(
            clients.Select(client => client.ToSummaryDto()).ToArray(),
            totalCount,
            request.PageNumber,
            request.PageSize);
    }

    public async Task<CarePlanDto> CreateCarePlanAsync(
        Guid clientId,
        CreateCarePlanRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdminCoordinatorOrClinician();
        await createCarePlanValidator.ValidateAndThrowAsync(request, cancellationToken);
        var client = await GetClientEntityAsync(clientId, cancellationToken);
        await EnsureCanWriteClientClinicalRecordAsync(client, cancellationToken);

        var activePlans = await unitOfWork.CarePlans.FindAsync(
            carePlan => carePlan.ClientId == client.Id && carePlan.IsActive,
            cancellationToken);
        foreach (var activePlan in activePlans)
        {
            activePlan.IsActive = false;
            await unitOfWork.CarePlans.UpdateAsync(activePlan, cancellationToken);
        }

        var carePlan = new CarePlan
        {
            ClientId = client.Id,
            Client = client,
            Title = request.Title.Trim(),
            Description = request.Description,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true,
            Goals = request.Goals,
            Interventions = request.Interventions,
            Notes = request.Notes,
        };

        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                await unitOfWork.CarePlans.AddAsync(carePlan, token);
                await unitOfWork.SaveChangesAsync(token);
                foreach (var activePlan in activePlans)
                {
                    await AuditAsync(ProtectedResourceType.CarePlan, activePlan.Id, AuditAction.Update, token);
                }

                await AuditAsync(ProtectedResourceType.CarePlan, carePlan.Id, AuditAction.Create, token);
            },
            cancellationToken);

        return carePlan.ToDto();
    }

    public async Task<CarePlanDto> UpdateCarePlanAsync(
        Guid carePlanId,
        UpdateCarePlanRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdminCoordinatorOrClinician();
        await updateCarePlanValidator.ValidateAndThrowAsync(request, cancellationToken);
        var carePlan = await GetCarePlanEntityAsync(carePlanId, cancellationToken);
        var client = await GetClientEntityAsync(carePlan.ClientId, cancellationToken);
        await EnsureCanWriteClientClinicalRecordAsync(client, cancellationToken);

        if (request.EndDate.HasValue && request.EndDate <= carePlan.StartDate)
        {
            throw new ValidationException("End date must be after start date.");
        }

        carePlan.Title = request.Title.Trim();
        carePlan.Description = request.Description;
        carePlan.EndDate = request.EndDate;
        carePlan.IsActive = request.IsActive;
        carePlan.Goals = request.Goals;
        carePlan.Interventions = request.Interventions;
        carePlan.Notes = request.Notes;

        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                await unitOfWork.CarePlans.UpdateAsync(carePlan, token);
                await unitOfWork.SaveChangesAsync(token);
                await AuditAsync(ProtectedResourceType.CarePlan, carePlan.Id, AuditAction.Update, token);
            },
            cancellationToken);

        return carePlan.ToDto();
    }

    public async Task<CarePlanDto> GetCarePlanAsync(
        Guid carePlanId,
        CancellationToken cancellationToken = default)
    {
        // The controller's CarePlan IDOR guard runs first (D-S6-14); this in-service check is
        // deliberate second-layer enforcement so the clinical read stays safe even if a future
        // call site bypasses the controller.
        var carePlan = await GetCarePlanEntityAsync(carePlanId, cancellationToken);
        await EnsureCanReadClientAsync(carePlan.ClientId, AccessScope.Full, cancellationToken);
        await AuditAsync(ProtectedResourceType.CarePlan, carePlan.Id, AuditAction.Read, cancellationToken);
        return carePlan.ToDto();
    }

    public async Task<PagedResult<CarePlanSummaryDto>> GetCarePlansAsync(
        Guid clientId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = await GetClientEntityAsync(clientId, cancellationToken);
        await EnsureCanReadClientAsync(client.Id, AccessScope.Full, cancellationToken);

        // D-S6-14: minimum-necessary summary rows only; the clinical text is served by the
        // audited per-plan detail read. Filtered, ordered, and paged at the repository.
        var (carePlans, totalCount) = await unitOfWork.CarePlans.GetPagedDescendingAsync(
            carePlan => carePlan.ClientId == client.Id,
            carePlan => carePlan.StartDate,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        foreach (var carePlan in carePlans)
        {
            await AuditAsync(ProtectedResourceType.CarePlan, carePlan.Id, AuditAction.Read, cancellationToken);
        }

        return PagedResultFactory.Create(
            carePlans.Select(carePlan => carePlan.ToSummaryDto()).ToArray(),
            totalCount,
            request.PageNumber,
            request.PageSize);
    }

    private async Task<IReadOnlyList<Client>> GetScopedClientsAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Array.Empty<Client>();
        }

        if (HasRole(ApplicationRoles.Client))
        {
            var clientsById = new Dictionary<Guid, Client>();
            var selfClients = await unitOfWork.Clients.FindAsync(
                client => client.UserId == currentUser.UserId.Value,
                cancellationToken);

            foreach (var client in selfClients)
            {
                clientsById[client.Id] = client;
            }

            var activeGrants = await unitOfWork.ClientAccessGrants.FindAsync(
                grant => grant.GranteeUserId == currentUser.UserId.Value
                    && grant.RevokedAtUtc == null
                    && (grant.AccessScope == AccessScope.PatientFacing || grant.AccessScope == AccessScope.Full),
                cancellationToken);
            var grantedClientIds = activeGrants.Select(grant => grant.ClientId).Distinct().ToArray();
            if (grantedClientIds.Length > 0)
            {
                var grantedClients = await unitOfWork.Clients.FindAsync(
                    client => grantedClientIds.Contains(client.Id),
                    cancellationToken);
                foreach (var client in grantedClients)
                {
                    clientsById[client.Id] = client;
                }
            }

            return clientsById.Values.OrderBy(client => client.Id).ToArray();
        }

        if (HasRole(ApplicationRoles.Caregiver))
        {
            var caregivers = await unitOfWork.Caregivers.FindAsync(
                caregiver => caregiver.UserId == currentUser.UserId.Value,
                cancellationToken);
            var caregiver = caregivers.SingleOrDefault();
            if (caregiver is null)
            {
                return Array.Empty<Client>();
            }

            var now = DateTime.UtcNow;
            var shifts = await unitOfWork.Shifts.FindAsync(
                shift => shift.CaregiverId == caregiver.Id
                    && shift.ScheduledStartTime <= now
                    && shift.ScheduledEndTime >= now
                    && (shift.Status == ShiftStatus.Scheduled || shift.Status == ShiftStatus.InProgress),
                cancellationToken);
            var clientIds = shifts.Select(shift => shift.ClientId).Distinct().ToArray();
            return clientIds.Length == 0
                ? Array.Empty<Client>()
                : await unitOfWork.Clients.FindAsync(client => clientIds.Contains(client.Id), cancellationToken);
        }

        if (HasRole(ApplicationRoles.Clinician))
        {
            var transitionPlans = await unitOfWork.TransitionPlans.FindAsync(
                plan => plan.Status != TransitionPlanStatus.Cancelled,
                cancellationToken);
            var clientIds = transitionPlans.Select(plan => plan.ClientId).Distinct().ToArray();
            return clientIds.Length == 0
                ? Array.Empty<Client>()
                : await unitOfWork.Clients.FindAsync(client => clientIds.Contains(client.Id), cancellationToken);
        }

        return Array.Empty<Client>();
    }

    private async Task EnsureCanReadClientAsync(
        Guid clientId,
        AccessScope requiredScope,
        CancellationToken cancellationToken)
    {
        if (HasAnyRole(ApplicationRoles.Admin, ApplicationRoles.Coordinator))
        {
            return;
        }

        if (HasRole(ApplicationRoles.Client) && currentUser.UserId.HasValue)
        {
            var grantResult = await clientAccessEvaluator.EvaluateAsync(
                currentUser.UserId.Value,
                clientId,
                requiredScope,
                cancellationToken);
            if (grantResult.IsAuthorized)
            {
                return;
            }
        }

        if (HasRole(ApplicationRoles.Caregiver) && await IsCurrentCaregiverAssignedToClientAsync(clientId, cancellationToken))
        {
            return;
        }

        if (HasRole(ApplicationRoles.Clinician)
            && await unitOfWork.TransitionPlans.ExistsAsync(
                plan => plan.ClientId == clientId && plan.Status != TransitionPlanStatus.Cancelled,
                cancellationToken))
        {
            return;
        }

        await AuditAsync(ProtectedResourceType.Client, clientId, AuditAction.AccessDenied, cancellationToken);
        throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
    }

    private async Task EnsureCanWriteClientClinicalRecordAsync(Client client, CancellationToken cancellationToken)
    {
        if (HasAnyRole(ApplicationRoles.Admin, ApplicationRoles.Coordinator))
        {
            return;
        }

        await AuditAsync(ProtectedResourceType.Client, client.Id, AuditAction.AccessDenied, cancellationToken);
        throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
    }

    private async Task<bool> IsCurrentCaregiverAssignedToClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return false;
        }

        var caregivers = await unitOfWork.Caregivers.FindAsync(
            caregiver => caregiver.UserId == currentUser.UserId.Value,
            cancellationToken);
        var caregiver = caregivers.SingleOrDefault();
        if (caregiver is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        return await unitOfWork.Shifts.ExistsAsync(
            shift => shift.CaregiverId == caregiver.Id
                && shift.ClientId == clientId
                && shift.ScheduledStartTime <= now
                && shift.ScheduledEndTime >= now
                && (shift.Status == ShiftStatus.Scheduled || shift.Status == ShiftStatus.InProgress),
            cancellationToken);
    }

    private async Task<Client> GetClientEntityAsync(Guid clientId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Clients.GetByIdAsync(clientId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task<CarePlan> GetCarePlanEntityAsync(Guid carePlanId, CancellationToken cancellationToken)
    {
        return await unitOfWork.CarePlans.GetByIdAsync(carePlanId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task<User> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task AttachUsersAsync(IReadOnlyList<Client> clients, CancellationToken cancellationToken)
    {
        foreach (var client in clients)
        {
            client.User = await GetUserAsync(client.UserId, cancellationToken);
        }
    }

    private void EnsureAdminOrCoordinator()
    {
        if (!HasAnyRole(ApplicationRoles.Admin, ApplicationRoles.Coordinator))
        {
            throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
        }
    }

    private void EnsureAdminCoordinatorOrClinician()
    {
        if (!HasAnyRole(ApplicationRoles.Admin, ApplicationRoles.Coordinator, ApplicationRoles.Clinician))
        {
            throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
        }
    }

    private bool HasAnyRole(params string[] roles) => roles.Any(HasRole);

    private bool HasRole(string role) => currentUser.Roles.Contains(role);

    private bool HasOperationalClientDetailAccess() =>
        HasAnyRole(ApplicationRoles.Admin, ApplicationRoles.Coordinator);

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
