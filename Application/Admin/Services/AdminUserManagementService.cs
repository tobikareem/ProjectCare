using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Admin.Validators;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Common.Paging;
using CarePath.Contracts.Admin;
using CarePath.Contracts.Common;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Interfaces.Repositories;
using FluentValidation;
using System.Data;
using ContractUserRole = CarePath.Contracts.Enumerations.UserRole;
using DomainUserRole = CarePath.Domain.Enumerations.UserRole;

namespace CarePath.Application.Admin.Services;

public sealed class AdminUserManagementService : IAdminUserManagementService
{
    private const string RoleInsufficient = "RoleInsufficient";
    private const string LastActiveAdmin = "Last active admin";
    private const string ProfileRoleCoupled = "Profile role coupled";

    private static readonly IReadOnlyList<ContractUserRole> AvailableRoles =
        Enum.GetValues<ContractUserRole>();

    private readonly IUnitOfWork unitOfWork;
    private readonly ICurrentUserContext currentUser;
    private readonly IIdentityProvisioningService identityProvisioning;
    private readonly IIdentityRoleManagementService identityRoleManagement;
    private readonly IPhiAuditLogger auditLogger;
    private readonly IValidator<CreateStaffUserRequest> createStaffUserValidator;
    private readonly IValidator<UpdateUserRoleRequest> updateRoleValidator;

    public AdminUserManagementService(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        IIdentityProvisioningService identityProvisioning,
        IIdentityRoleManagementService identityRoleManagement,
        IPhiAuditLogger auditLogger,
        IValidator<CreateStaffUserRequest>? createStaffUserValidator = null,
        IValidator<UpdateUserRoleRequest>? updateRoleValidator = null)
    {
        this.unitOfWork = unitOfWork;
        this.currentUser = currentUser;
        this.identityProvisioning = identityProvisioning;
        this.identityRoleManagement = identityRoleManagement;
        this.auditLogger = auditLogger;
        this.createStaffUserValidator = createStaffUserValidator ?? new CreateStaffUserRequestValidator();
        this.updateRoleValidator = updateRoleValidator ?? new UpdateUserRoleRequestValidator();
    }

    public async Task<PagedResult<UserAccountDto>> GetUsersAsync(
        PagedRequest request,
        ContractUserRole? role = null,
        bool? isActive = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (role.HasValue && !Enum.IsDefined(role.Value))
        {
            throw new ValidationException("The request is invalid.");
        }

        await EnsureCurrentActorIsActiveAdminAsync(cancellationToken);

        DomainUserRole? roleFilter = role.HasValue ? (DomainUserRole)(int)role.Value : null;
        var searchText = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();

        // Filtered + paged at the repository (deterministic LastName/FirstName/Id order);
        // search matches email and display name ONLY per D-S6-8 — never phone, address,
        // or any PHI field.
        var (users, totalCount) = await unitOfWork.Users.GetPagedAsync(
            user =>
                (!roleFilter.HasValue || user.Role == roleFilter.Value) &&
                (!isActive.HasValue || user.IsActive == isActive.Value) &&
                (searchText == null
                    || user.Email.ToLower().Contains(searchText)
                    || (user.FirstName + " " + user.LastName).ToLower().Contains(searchText)),
            user => user.LastName,
            user => user.FirstName,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return PagedResultFactory.Create(
            await ToDtosAsync(users, cancellationToken),
            totalCount,
            request.PageNumber,
            request.PageSize);
    }

    public async Task<IReadOnlyList<ContractUserRole>> GetAvailableRolesAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureCurrentActorIsActiveAdminAsync(cancellationToken);
        return AvailableRoles;
    }

    public async Task<UserAccountDto> CreateStaffUserAsync(
        CreateStaffUserRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureCurrentActorIsActiveAdminAsync(cancellationToken);
        await createStaffUserValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (await unitOfWork.Users.ExistsAsync(user => user.Email == request.Email.Trim(), cancellationToken))
        {
            throw new ValidationException("The request is invalid.");
        }

        var role = (DomainUserRole)(int)request.Role;
        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Role = role,
            IsActive = true,
        };

        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                await unitOfWork.Users.AddAsync(user, token);
                await unitOfWork.SaveChangesAsync(token);

                var provisionResult = await identityProvisioning.ProvisionUserAsync(
                    new IdentityProvisioningRequest(
                        user.Id,
                        user.Email,
                        user.PhoneNumber,
                        request.TemporaryPassword!,
                        role.ToString()),
                    token);

                if (!provisionResult.Succeeded)
                {
                    throw new ValidationException("The account could not be provisioned.");
                }

                await AuditAsync(
                    AuditAction.StaffProvisioned,
                    user.Id,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["Role"] = role.ToString(),
                    },
                    token);
            },
            cancellationToken);

        return await ToDtoAsync(user, cancellationToken);
    }

    public async Task<UserAccountDto> UpdateRoleAsync(
        Guid userId,
        UpdateUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        await updateRoleValidator.ValidateAndThrowAsync(request, cancellationToken);

        var newRole = (DomainUserRole)(int)request.Role;

        return await unitOfWork.ExecuteInTransactionAsync(
            IsolationLevel.Serializable,
            async token =>
            {
                await EnsureCurrentActorIsActiveAdminAsync(token);

                var user = await GetUserAsync(userId, token);
                var oldRole = user.Role;
                await EnsureRoleCanChangeAsync(user, newRole, token);

                if (oldRole == newRole)
                {
                    return await ToDtoAsync(user, token);
                }

                user.Role = newRole;
                await unitOfWork.Users.UpdateAsync(user, token);
                await unitOfWork.SaveChangesAsync(token);

                if (!await identityRoleManagement.ReplaceUserRoleAsync(user.Id, newRole.ToString(), token))
                {
                    throw new ResourceConflictException("identity.role_sync_failed", "The account role could not be updated.");
                }

                await AuditAsync(
                    AuditAction.RoleChanged,
                    user.Id,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["OldRole"] = oldRole.ToString(),
                        ["NewRole"] = newRole.ToString(),
                    },
                    token);

                return await ToDtoAsync(user, token);
            },
            cancellationToken);
    }

    public async Task<UserAccountDto> UpdateStatusAsync(
        Guid userId,
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        return await unitOfWork.ExecuteInTransactionAsync(
            IsolationLevel.Serializable,
            async token =>
            {
                await EnsureCurrentActorIsActiveAdminAsync(token);

                var user = await GetUserAsync(userId, token);
                if (!request.IsActive && await IsLastActiveAdminAsync(user, token))
                {
                    throw new ResourceConflictException("admin.last_active_admin", LastActiveAdmin);
                }

                if (user.IsActive == request.IsActive)
                {
                    return await ToDtoAsync(user, token);
                }

                user.IsActive = request.IsActive;
                await unitOfWork.Users.UpdateAsync(user, token);
                await unitOfWork.SaveChangesAsync(token);

                await AuditAsync(
                    request.IsActive ? AuditAction.AccountActivated : AuditAction.AccountDeactivated,
                    user.Id,
                    token);

                return await ToDtoAsync(user, token);
            },
            cancellationToken);
    }

    private async Task EnsureCurrentActorIsActiveAdminAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            throw new ResourceAccessDeniedException(RoleInsufficient, isPhiResource: false);
        }

        var actor = await unitOfWork.Users.GetByIdAsync(currentUser.UserId.Value, cancellationToken);
        if (actor is not { IsActive: true, Role: DomainUserRole.Admin })
        {
            throw new ResourceAccessDeniedException(RoleInsufficient, isPhiResource: false);
        }
    }

    private async Task EnsureRoleCanChangeAsync(
        User user,
        DomainUserRole newRole,
        CancellationToken cancellationToken)
    {
        if (await IsLastActiveAdminAsync(user, cancellationToken) && newRole != DomainUserRole.Admin)
        {
            throw new ResourceConflictException("admin.last_active_admin", LastActiveAdmin);
        }

        if (await HasCaregiverProfileAsync(user.Id, cancellationToken) && newRole != DomainUserRole.Caregiver)
        {
            throw new ResourceConflictException("admin.profile_role_coupled", ProfileRoleCoupled);
        }

        if (await HasClientProfileAsync(user.Id, cancellationToken) && newRole != DomainUserRole.Client)
        {
            throw new ResourceConflictException("admin.profile_role_coupled", ProfileRoleCoupled);
        }
    }

    private async Task<User> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: false);
    }

    private async Task<IReadOnlyList<UserAccountDto>> ToDtosAsync(
        IReadOnlyList<User> users,
        CancellationToken cancellationToken)
    {
        if (users.Count == 0)
        {
            return Array.Empty<UserAccountDto>();
        }

        var userIds = users.Select(user => user.Id).ToArray();
        var caregiverProfiles = await unitOfWork.Caregivers.FindAsync(
            caregiver => userIds.Contains(caregiver.UserId),
            cancellationToken);
        var clientProfiles = await unitOfWork.Clients.FindAsync(
            client => userIds.Contains(client.UserId),
            cancellationToken);
        var caregiverUserIds = caregiverProfiles.Select(caregiver => caregiver.UserId).ToHashSet();
        var clientUserIds = clientProfiles.Select(client => client.UserId).ToHashSet();
        var activeAdminCount = await CountActiveAdminsAsync(cancellationToken);

        return users
            .Select(user => ToDto(
                user,
                caregiverUserIds.Contains(user.Id),
                clientUserIds.Contains(user.Id),
                IsLastActiveAdmin(user, activeAdminCount)))
            .ToArray();
    }

    private async Task<UserAccountDto> ToDtoAsync(User user, CancellationToken cancellationToken)
    {
        var hasCaregiverProfile = await HasCaregiverProfileAsync(user.Id, cancellationToken);
        var hasClientProfile = await HasClientProfileAsync(user.Id, cancellationToken);
        var lastActiveAdmin = await IsLastActiveAdminAsync(user, cancellationToken);
        return ToDto(user, hasCaregiverProfile, hasClientProfile, lastActiveAdmin);
    }

    private static UserAccountDto ToDto(
        User user,
        bool hasCaregiverProfile,
        bool hasClientProfile,
        bool lastActiveAdmin)
    {
        var disabledReason = DisabledReason(lastActiveAdmin, hasCaregiverProfile, hasClientProfile);

        return new UserAccountDto
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.FullName,
            Role = (ContractUserRole)(int)user.Role,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt,
            HasCaregiverProfile = hasCaregiverProfile,
            HasClientProfile = hasClientProfile,
            CanChangeRole = disabledReason is null,
            CanDeactivate = !lastActiveAdmin,
            DisabledReason = disabledReason,
        };
    }

    private static string? DisabledReason(
        bool lastActiveAdmin,
        bool hasCaregiverProfile,
        bool hasClientProfile)
    {
        if (lastActiveAdmin)
        {
            return LastActiveAdmin;
        }

        if (hasCaregiverProfile || hasClientProfile)
        {
            return ProfileRoleCoupled;
        }

        return null;
    }

    private Task<bool> HasCaregiverProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        unitOfWork.Caregivers.ExistsAsync(caregiver => caregiver.UserId == userId, cancellationToken);

    private Task<bool> HasClientProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        unitOfWork.Clients.ExistsAsync(client => client.UserId == userId, cancellationToken);

    private async Task<bool> IsLastActiveAdminAsync(User user, CancellationToken cancellationToken) =>
        user is { IsActive: true, Role: DomainUserRole.Admin }
            && IsLastActiveAdmin(user, await CountActiveAdminsAsync(cancellationToken));

    private static bool IsLastActiveAdmin(User user, int activeAdminCount) =>
        user is { IsActive: true, Role: DomainUserRole.Admin } && activeAdminCount <= 1;

    private Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken) =>
        unitOfWork.Users.CountAsync(
            candidate => candidate.IsActive && candidate.Role == DomainUserRole.Admin,
            cancellationToken);

    private Task AuditAsync(
        AuditAction action,
        Guid targetUserId,
        CancellationToken cancellationToken) =>
        AuditAsync(action, targetUserId, attributes: null, cancellationToken);

    private Task AuditAsync(
        AuditAction action,
        Guid targetUserId,
        IReadOnlyDictionary<string, string>? attributes,
        CancellationToken cancellationToken)
    {
        return auditLogger.LogAsync(
            new PhiAuditEntry(
                currentUser.UserId,
                currentUser.UserId.HasValue ? AuditActorType.User : AuditActorType.Anonymous,
                DateTime.UtcNow,
                action,
                ProtectedResourceType.UserAccount,
                targetUserId,
                currentUser.CorrelationId,
                Attributes: attributes),
            cancellationToken);
    }
}
