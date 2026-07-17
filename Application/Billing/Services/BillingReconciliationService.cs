using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Billing;
using CarePath.Application.Billing.Validators;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Common.Mapping;
using CarePath.Contracts.Billing;
using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentValidation;
using DomainReconciliationReason = CarePath.Domain.Enumerations.BillingReconciliationReason;

namespace CarePath.Application.Billing.Services;

/// <summary>
/// D-S6-18 reconciliation implementation. All classification flows through the shared
/// <see cref="IBillingEligibilityQuery"/>; resolutions are append-only; every PHI read and
/// mutation emits ID-only audit events; no display values enter logs or errors.
/// </summary>
public sealed class BillingReconciliationService : IBillingReconciliationService
{
    private const string AlreadyInvoicedCode = "reconciliation.already_invoiced";
    private const string AlreadyResolvedCode = "reconciliation.already_resolved";
    private const string NotResolvedCode = "reconciliation.not_resolved";
    private const string NotTimeCorrectableCode = "reconciliation.not_time_correctable";
    private const string WindowImplausibleCode = "reconciliation.window_implausible";

    /// <summary>Allowed drift between corrected actuals and the scheduled window, in hours.</summary>
    public const int CorrectionWindowSlackHours = 24;

    private readonly IUnitOfWork unitOfWork;
    private readonly ICurrentUserContext currentUser;
    private readonly IPhiAuditLogger auditLogger;
    private readonly IBillingEligibilityQuery eligibilityQuery;
    private readonly IBillingReconciliationStore store;
    private readonly IValidator<BillingReconciliationSearchRequest> searchValidator;
    private readonly IValidator<ResolveNonBillableRequest> resolveValidator;
    private readonly IValidator<ReopenResolutionRequest> reopenValidator;
    private readonly IValidator<CorrectShiftTimeRequest> correctTimeValidator;

    public BillingReconciliationService(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        IPhiAuditLogger auditLogger,
        IBillingEligibilityQuery eligibilityQuery,
        IBillingReconciliationStore store,
        IValidator<BillingReconciliationSearchRequest>? searchValidator = null,
        IValidator<ResolveNonBillableRequest>? resolveValidator = null,
        IValidator<ReopenResolutionRequest>? reopenValidator = null,
        IValidator<CorrectShiftTimeRequest>? correctTimeValidator = null)
    {
        this.unitOfWork = unitOfWork;
        this.currentUser = currentUser;
        this.auditLogger = auditLogger;
        this.eligibilityQuery = eligibilityQuery;
        this.store = store;
        this.searchValidator = searchValidator ?? new BillingReconciliationSearchRequestValidator();
        this.resolveValidator = resolveValidator ?? new ResolveNonBillableRequestValidator();
        this.reopenValidator = reopenValidator ?? new ReopenResolutionRequestValidator();
        this.correctTimeValidator = correctTimeValidator ?? new CorrectShiftTimeRequestValidator();
    }

    public async Task<BillingReconciliationSearchResponseDto> SearchAsync(
        BillingReconciliationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        await searchValidator.ValidateAndThrowAsync(request, cancellationToken);

        var utcNow = DateTime.UtcNow;
        var page = await eligibilityQuery.SearchReconciliationAsync(
            new ReconciliationSearchCriteria(
                request.PeriodStartUtc,
                request.PeriodEndUtc,
                request.ClientId,
                request.ServiceType.HasValue ? (ServiceType)(int)request.ServiceType.Value : null,
                request.Reason.HasValue ? (BillingExclusionReason)(int)request.Reason.Value : null,
                request.AgedRiskOnly,
                request.PageNumber,
                request.PageSize,
                utcNow),
            cancellationToken);

        foreach (var row in page.Rows)
        {
            await AuditAsync(ProtectedResourceType.Shift, row.ShiftId, AuditAction.Read, cancellationToken);
        }

        return new BillingReconciliationSearchResponseDto
        {
            Rows = page.Rows.Select(row => row.ToReconciliationRowDto(utcNow)).ToArray(),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = page.TotalCount,
            Kpis = new BillingReconciliationKpiDto
            {
                UnresolvedCount = page.Kpis.UnresolvedCount,
                RevenueAtRiskValue = page.Kpis.RevenueAtRiskValue,
                AgedCount = page.Kpis.AgedCount,
                AgedValue = page.Kpis.AgedValue,
            },
        };
    }

    public async Task<BillingReconciliationDetailDto> GetDetailAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        var row = await GetRowAsync(shiftId, cancellationToken);
        await AuditAsync(ProtectedResourceType.Shift, row.ShiftId, AuditAction.Read, cancellationToken);
        return await ComposeDetailAsync(row, cancellationToken);
    }

    public async Task<BillingReconciliationDetailDto> ResolveAsync(
        Guid shiftId,
        ResolveNonBillableRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        await resolveValidator.ValidateAndThrowAsync(request, cancellationToken);

        var row = await GetRowAsync(shiftId, cancellationToken);
        if (row.Reason == BillingExclusionReason.AlreadyInvoiced)
        {
            throw new ResourceConflictException(AlreadyInvoicedCode, "The service is already invoiced and cannot be resolved as non-billable.");
        }

        if (row.Reason == BillingExclusionReason.NonBillableResolved)
        {
            throw new ResourceConflictException(AlreadyResolvedCode, "The service already has an active non-billable resolution.");
        }

        var resolution = new BillingReconciliationResolution
        {
            ShiftId = shiftId,
            Reason = (DomainReconciliationReason)(int)request.Reason,
            ResolvedByUserId = RequireUserId(),
            ResolvedAtUtc = DateTime.UtcNow,
            Note = TrimToNull(request.Note),
        };

        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                await store.AppendAsync(resolution, token);
                await AuditAsync(ProtectedResourceType.Shift, shiftId, AuditAction.Update, token);
            },
            cancellationToken);

        return await GetDetailAfterMutationAsync(shiftId, cancellationToken);
    }

    public async Task<BillingReconciliationDetailDto> ReopenAsync(
        Guid shiftId,
        ReopenResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        await reopenValidator.ValidateAndThrowAsync(request, cancellationToken);

        _ = await GetRowAsync(shiftId, cancellationToken);
        var latest = await store.GetLatestAsync(shiftId, cancellationToken);
        if (latest is null || latest.Reason == DomainReconciliationReason.Reopened)
        {
            throw new ResourceConflictException(NotResolvedCode, "The service has no active resolution to reopen.");
        }

        var reopen = new BillingReconciliationResolution
        {
            ShiftId = shiftId,
            Reason = DomainReconciliationReason.Reopened,
            ResolvedByUserId = RequireUserId(),
            ResolvedAtUtc = DateTime.UtcNow,
            Note = TrimToNull(request.Note),
            SupersedesResolutionId = latest.Id,
        };

        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                await store.AppendAsync(reopen, token);
                await AuditAsync(ProtectedResourceType.Shift, shiftId, AuditAction.Update, token);
            },
            cancellationToken);

        return await GetDetailAfterMutationAsync(shiftId, cancellationToken);
    }

    public async Task<BillingReconciliationDetailDto> CorrectTimeAsync(
        Guid shiftId,
        CorrectShiftTimeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        await correctTimeValidator.ValidateAndThrowAsync(request, cancellationToken);

        var row = await GetRowAsync(shiftId, cancellationToken);
        if (row.Reason is not (BillingExclusionReason.MissingActualTime or BillingExclusionReason.InvalidBillableTime))
        {
            throw new ResourceConflictException(NotTimeCorrectableCode, "The service is not eligible for a time correction.");
        }

        // Money-path sanity bound: the corrected actual window must sit near the scheduled
        // window — corrected hours flow straight into invoice totals.
        if (request.ActualStartUtc < row.ScheduledStartUtc.AddHours(-CorrectionWindowSlackHours)
            || request.ActualEndUtc > row.ScheduledEndUtc.AddHours(CorrectionWindowSlackHours))
        {
            throw new ResourceConflictException(WindowImplausibleCode, "The corrected window is too far from the scheduled service window.");
        }

        var shift = await unitOfWork.Shifts.GetByIdAsync(shiftId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
        shift.ActualStartTime = request.ActualStartUtc;
        shift.ActualEndTime = request.ActualEndUtc;
        shift.BreakMinutes = request.BreakMinutes;

        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                await unitOfWork.Shifts.UpdateAsync(shift, token);
                await unitOfWork.SaveChangesAsync(token);
                await auditLogger.LogAsync(
                    new PhiAuditEntry(
                        currentUser.UserId,
                        currentUser.UserId.HasValue ? AuditActorType.User : AuditActorType.Anonymous,
                        DateTime.UtcNow,
                        AuditAction.Update,
                        ProtectedResourceType.Shift,
                        shiftId,
                        currentUser.CorrelationId,
                        Attributes: new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["TimeCorrectionReason"] = request.Reason.ToString(),
                        }),
                    token);
            },
            cancellationToken);

        return await GetDetailAfterMutationAsync(shiftId, cancellationToken);
    }

    private async Task<BillingReconciliationDetailDto> GetDetailAfterMutationAsync(
        Guid shiftId,
        CancellationToken cancellationToken)
    {
        var row = await GetRowAsync(shiftId, cancellationToken);
        return await ComposeDetailAsync(row, cancellationToken);
    }

    private async Task<BillingEligibilityRow> GetRowAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        return await eligibilityQuery.GetShiftRowAsync(shiftId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task<BillingReconciliationDetailDto> ComposeDetailAsync(
        BillingEligibilityRow row,
        CancellationToken cancellationToken)
    {
        var history = await store.GetHistoryAsync(row.ShiftId, cancellationToken);
        var supersededIds = history
            .Where(resolution => resolution.SupersedesResolutionId.HasValue)
            .Select(resolution => resolution.SupersedesResolutionId!.Value)
            .ToHashSet();
        var resolverIds = history.Select(resolution => resolution.ResolvedByUserId).Distinct().ToArray();
        var resolvers = resolverIds.Length == 0
            ? []
            : await unitOfWork.Users.FindAsync(user => resolverIds.Contains(user.Id), cancellationToken);
        var resolverNames = resolvers.ToDictionary(user => user.Id, user => user.FullName);

        // The caller's single Shift read/update audit covers the disclosure; per-record
        // audits here would emit N duplicate rows for the same ShiftId on every detail view.
        var records = new List<BillingResolutionRecordDto>(history.Count);
        foreach (var resolution in history)
        {
            records.Add(resolution.ToRecordDto(
                resolverNames.GetValueOrDefault(resolution.ResolvedByUserId, string.Empty),
                supersededIds.Contains(resolution.Id)));
        }

        return new BillingReconciliationDetailDto
        {
            Row = row.ToReconciliationRowDto(DateTime.UtcNow),
            ResolutionHistory = records,
        };
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new ResourceAccessDeniedException("Unauthenticated", isPhiResource: true);

    private void EnsureAdminOrCoordinator()
    {
        if (!currentUser.Roles.Contains(ApplicationRoles.Admin)
            && !currentUser.Roles.Contains(ApplicationRoles.Coordinator))
        {
            throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
        }
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
