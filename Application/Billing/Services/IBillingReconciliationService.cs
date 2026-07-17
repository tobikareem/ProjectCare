using CarePath.Contracts.Billing;

namespace CarePath.Application.Billing.Services;

/// <summary>
/// D-S6-18 revenue-leakage reconciliation: bounded body-based search with full-filter KPIs,
/// guarded per-shift detail, append-only non-billable resolve/reopen, and the dedicated
/// audited missing-time correction. Admin/Coordinator only.
/// </summary>
public interface IBillingReconciliationService
{
    Task<BillingReconciliationSearchResponseDto> SearchAsync(
        BillingReconciliationSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<BillingReconciliationDetailDto> GetDetailAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default);

    Task<BillingReconciliationDetailDto> ResolveAsync(
        Guid shiftId,
        ResolveNonBillableRequest request,
        CancellationToken cancellationToken = default);

    Task<BillingReconciliationDetailDto> ReopenAsync(
        Guid shiftId,
        ReopenResolutionRequest request,
        CancellationToken cancellationToken = default);

    Task<BillingReconciliationDetailDto> CorrectTimeAsync(
        Guid shiftId,
        CorrectShiftTimeRequest request,
        CancellationToken cancellationToken = default);
}
