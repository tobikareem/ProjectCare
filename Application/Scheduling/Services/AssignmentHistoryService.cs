using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Scheduling.Queries;
using CarePath.Application.Scheduling.Validators;
using CarePath.Contracts.Common;
using CarePath.Contracts.Scheduling;
using CarePath.Domain.Interfaces.Repositories;
using FluentValidation;
using System.Security.Cryptography;

namespace CarePath.Application.Scheduling.Services;

public sealed class AssignmentHistoryService : IAssignmentHistoryService
{
    private readonly IAssignmentHistoryQuery query;
    private readonly IUnitOfWork unitOfWork;
    private readonly ICurrentUserContext currentUser;
    private readonly IPhiAuditLogger auditLogger;
    private readonly IValidator<AssignmentHistorySearchRequest> validator;

    public AssignmentHistoryService(
        IAssignmentHistoryQuery query,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        IPhiAuditLogger auditLogger,
        IValidator<AssignmentHistorySearchRequest>? validator = null)
    {
        this.query = query;
        this.unitOfWork = unitOfWork;
        this.currentUser = currentUser;
        this.auditLogger = auditLogger;
        this.validator = validator ?? new AssignmentHistorySearchRequestValidator();
    }

    public async Task<PagedResult<CaregiverAssignmentSummaryDto>> GetCaregiversForClientAsync(
        Guid clientId,
        AssignmentHistorySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureStaff();
        var normalized = await ValidateAndNormalizeAsync(request, cancellationToken);
        var result = await query.GetCaregiversForClientAsync(clientId, normalized, DateTime.UtcNow, cancellationToken);

        await AuditAsync(ProtectedResourceType.Client, clientId, cancellationToken);
        foreach (var item in result.Items)
        {
            await AuditAsync(ProtectedResourceType.Caregiver, item.CaregiverId, cancellationToken);
            await AuditAsync(ProtectedResourceType.AssignmentRelationship, RelationshipId(clientId, item.CaregiverId), cancellationToken);
        }

        return result;
    }

    public async Task<PagedResult<ClientAssignmentSummaryDto>> GetClientsForCaregiverAsync(
        Guid caregiverId,
        AssignmentHistorySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureStaff();
        var normalized = await ValidateAndNormalizeAsync(request, cancellationToken);
        var result = await query.GetClientsForCaregiverAsync(caregiverId, normalized, DateTime.UtcNow, cancellationToken);

        await AuditAsync(ProtectedResourceType.Caregiver, caregiverId, cancellationToken);
        foreach (var item in result.Items)
        {
            await AuditAsync(ProtectedResourceType.Client, item.ClientId, cancellationToken);
            await AuditAsync(ProtectedResourceType.AssignmentRelationship, RelationshipId(item.ClientId, caregiverId), cancellationToken);
        }

        return result;
    }

    public async Task<PagedResult<MyClientAssignmentSummaryDto>> GetMyClientsAsync(
        AssignmentHistorySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.Roles.Contains(ApplicationRoles.Caregiver) || !currentUser.UserId.HasValue)
        {
            throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
        }

        var profiles = await unitOfWork.Caregivers.FindAsync(
            caregiver => caregiver.UserId == currentUser.UserId.Value,
            cancellationToken);
        var caregiver = profiles.SingleOrDefault() ?? throw new ResourceNotFoundException();
        var normalized = await ValidateAndNormalizeAsync(request, cancellationToken);
        var result = await query.GetClientsForCaregiverAsync(caregiver.Id, normalized, DateTime.UtcNow, cancellationToken);

        await AuditAsync(ProtectedResourceType.Caregiver, caregiver.Id, cancellationToken);
        foreach (var item in result.Items)
        {
            await AuditAsync(ProtectedResourceType.Client, item.ClientId, cancellationToken);
            await AuditAsync(ProtectedResourceType.AssignmentRelationship, RelationshipId(item.ClientId, caregiver.Id), cancellationToken);
        }

        return new PagedResult<MyClientAssignmentSummaryDto>
        {
            Items = result.Items.Select(item => new MyClientAssignmentSummaryDto(
                AbbreviateName(item.ClientDisplayName),
                item.ServiceType,
                item.FirstAssignedAtUtc,
                item.LastAssignedAtUtc,
                item.NextShiftAtUtc,
                item.CompletedShiftCount,
                item.Status)).ToArray(),
            PageNumber = result.PageNumber,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        };
    }

    public async Task<PagedResult<MyCaregiverAssignmentSummaryDto>> GetMyCaregiversAsync(
        AssignmentHistorySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.Roles.Contains(ApplicationRoles.Client) || !currentUser.UserId.HasValue)
        {
            throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
        }

        var profiles = await unitOfWork.Clients.FindAsync(
            client => client.UserId == currentUser.UserId.Value,
            cancellationToken);
        var client = profiles.SingleOrDefault() ?? throw new ResourceNotFoundException();
        var normalized = await ValidateAndNormalizeAsync(request, cancellationToken);
        var result = await query.GetCaregiversForClientAsync(client.Id, normalized, DateTime.UtcNow, cancellationToken);

        await AuditAsync(ProtectedResourceType.Client, client.Id, cancellationToken);
        foreach (var item in result.Items)
        {
            await AuditAsync(ProtectedResourceType.Caregiver, item.CaregiverId, cancellationToken);
            await AuditAsync(ProtectedResourceType.AssignmentRelationship, RelationshipId(client.Id, item.CaregiverId), cancellationToken);
        }

        return new PagedResult<MyCaregiverAssignmentSummaryDto>
        {
            Items = result.Items.Select(item => new MyCaregiverAssignmentSummaryDto(
                AbbreviateName(item.CaregiverDisplayName),
                item.FirstAssignedAtUtc,
                item.LastAssignedAtUtc,
                item.NextShiftAtUtc,
                item.Status)).ToArray(),
            PageNumber = result.PageNumber,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        };
    }

    private async Task<AssignmentHistorySearchRequest> ValidateAndNormalizeAsync(
        AssignmentHistorySearchRequest request,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return new AssignmentHistorySearchRequest
        {
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            SearchTerm = string.IsNullOrWhiteSpace(request.SearchTerm) ? null : request.SearchTerm.Trim(),
            Status = request.Status,
            FromUtc = NormalizeUtc(request.FromUtc),
            ToUtc = NormalizeUtc(request.ToUtc),
        };
    }

    private void EnsureStaff()
    {
        if (!currentUser.Roles.Contains(ApplicationRoles.Admin) &&
            !currentUser.Roles.Contains(ApplicationRoles.Coordinator))
        {
            throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
        }
    }

    private Task AuditAsync(ProtectedResourceType resourceType, Guid resourceId, CancellationToken cancellationToken) =>
        auditLogger.LogAsync(new PhiAuditEntry(
            currentUser.UserId,
            currentUser.UserId.HasValue ? AuditActorType.User : AuditActorType.Anonymous,
            DateTime.UtcNow,
            AuditAction.Read,
            resourceType,
            resourceId,
            currentUser.CorrelationId), cancellationToken);

    private static DateTime? NormalizeUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } utc => utc,
        { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
        { } unspecified => DateTime.SpecifyKind(unspecified, DateTimeKind.Utc),
    };

    private static string AbbreviateName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => "Client",
            1 => parts[0],
            _ => $"{parts[0]} {parts[^1][0]}.",
        };
    }

    private static Guid RelationshipId(Guid clientId, Guid caregiverId)
    {
        Span<byte> input = stackalloc byte[32];
        clientId.TryWriteBytes(input[..16]);
        caregiverId.TryWriteBytes(input[16..]);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return new Guid(hash[..16]);
    }
}
