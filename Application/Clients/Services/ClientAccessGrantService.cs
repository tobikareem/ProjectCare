using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Clients.Validators;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Common.Mapping;
using CarePath.Contracts.Clients;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentValidation;

namespace CarePath.Application.Clients.Services;

public sealed class ClientAccessGrantService : IClientAccessGrantService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ICurrentUserContext currentUser;
    private readonly IPhiAuditLogger auditLogger;
    private readonly IValidator<CreateGrantRequest> createValidator;

    public ClientAccessGrantService(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        IPhiAuditLogger auditLogger,
        IValidator<CreateGrantRequest>? createValidator = null)
    {
        this.unitOfWork = unitOfWork;
        this.currentUser = currentUser;
        this.auditLogger = auditLogger;
        this.createValidator = createValidator ?? new CreateGrantRequestValidator();
    }

    public async Task<IReadOnlyList<ClientAccessGrantDto>> GetGrantsAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        _ = await GetClientAsync(clientId, cancellationToken);
        var grants = await unitOfWork.ClientAccessGrants.FindAsync(grant => grant.ClientId == clientId && grant.RevokedAtUtc == null, cancellationToken);
        foreach (var grant in grants)
        {
            grant.GranteeUser = await GetUserAsync(grant.GranteeUserId, cancellationToken);
            await AuditAsync(grant.Id, AuditAction.Read, cancellationToken);
        }

        return grants.Select(grant => grant.ToDto()).ToArray();
    }

    public async Task<ClientAccessGrantDto> CreateGrantAsync(Guid clientId, CreateGrantRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        _ = await GetClientAsync(clientId, cancellationToken);
        var grantee = await GetUserAsync(request.GranteeUserId, cancellationToken);
        if (grantee.Role != UserRole.Client)
        {
            throw new ValidationException("The request is invalid.");
        }

        var existing = await unitOfWork.ClientAccessGrants.FindAsync(
            grant => grant.ClientId == clientId && grant.GranteeUserId == request.GranteeUserId && grant.RevokedAtUtc == null,
            cancellationToken);
        if (existing.Count > 0)
        {
            throw new ValidationException("The request is invalid.");
        }

        var grant = new ClientAccessGrant
        {
            ClientId = clientId,
            GranteeUserId = request.GranteeUserId,
            GranteeUser = grantee,
            GrantedByUserId = currentUser.UserId ?? throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true),
            AccessScope = (AccessScope)(int)request.Scope,
            GrantedAtUtc = DateTime.UtcNow,
        };

        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                await unitOfWork.ClientAccessGrants.AddAsync(grant, token);
                await unitOfWork.SaveChangesAsync(token);
                await AuditAsync(grant.Id, AuditAction.Create, token);
            },
            cancellationToken);

        return grant.ToDto();
    }

    public async Task RevokeGrantAsync(Guid clientId, Guid grantId, CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        _ = await GetClientAsync(clientId, cancellationToken);
        var grant = await unitOfWork.ClientAccessGrants.GetByIdAsync(grantId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
        if (grant.ClientId != clientId)
        {
            throw new ResourceNotFoundException(isPhiResource: true);
        }

        grant.RevokedAtUtc = DateTime.UtcNow;
        grant.RevokedByUserId = currentUser.UserId;

        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                await unitOfWork.ClientAccessGrants.UpdateAsync(grant, token);
                await unitOfWork.SaveChangesAsync(token);
                await AuditAsync(grant.Id, AuditAction.Delete, token);
            },
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

    private void EnsureAdminOrCoordinator()
    {
        if (!currentUser.Roles.Contains(ApplicationRoles.Admin) && !currentUser.Roles.Contains(ApplicationRoles.Coordinator))
        {
            throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
        }
    }

    private Task AuditAsync(Guid grantId, AuditAction action, CancellationToken cancellationToken)
    {
        return auditLogger.LogAsync(
            new PhiAuditEntry(
                currentUser.UserId,
                currentUser.UserId.HasValue ? AuditActorType.User : AuditActorType.Anonymous,
                DateTime.UtcNow,
                action,
                ProtectedResourceType.ClientAccessGrant,
                grantId,
                currentUser.CorrelationId),
            cancellationToken);
    }
}
