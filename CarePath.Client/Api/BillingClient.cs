using CarePath.Client.Http;
using CarePath.Contracts.Billing;
using CarePath.Contracts.Common;

namespace CarePath.Client.Api;

/// <summary>
/// Typed client for <c>/api/invoices</c> and the Admin-only <c>/api/billing/margins</c>
/// endpoints (D-S4-2). Margin methods return compensation data and succeed only for Admin users.
/// </summary>
public sealed class BillingClient : ApiClientBase
{
    /// <summary>Creates the client.</summary>
    /// <param name="httpClient">HTTP client configured with the API base address.</param>
    public BillingClient(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>Gets a page of invoices (Admin/Coordinator).</summary>
    /// <param name="paging">Paging parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The paged invoice list.</returns>
    public Task<ApiResponse<PagedResult<InvoiceSummaryDto>>> GetInvoicesAsync(
        PagedRequest paging,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<InvoiceSummaryDto>>($"api/invoices?{paging.ToQueryString()}", cancellationToken);

    /// <summary>Gets an invoice by ID.</summary>
    /// <param name="invoiceId">Invoice identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The invoice detail.</returns>
    public Task<ApiResponse<InvoiceDetailDto>> GetInvoiceAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default) =>
        GetAsync<InvoiceDetailDto>($"api/invoices/{invoiceId}", cancellationToken);

    /// <summary>
    /// Previews an invoice for a client/service line/period (Admin/Coordinator, D-S6-18).
    /// Returns paged eligible rows, full-set aggregates, exclusion counts, and the opaque
    /// expiring preview token required by <see cref="CreateInvoiceAsync"/>. The token must
    /// never be logged or stored beyond the generate flow.
    /// </summary>
    /// <param name="request">The preview selection + paging.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The preview result.</returns>
    public Task<ApiResponse<InvoicePreviewResponseDto>> PreviewInvoiceAsync(
        InvoicePreviewRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<InvoicePreviewRequest, InvoicePreviewResponseDto>(
            "api/invoices/preview", request, cancellationToken);

    /// <summary>
    /// Creates a period invoice (Admin/Coordinator; preview-gated per D-S6-18 — the request
    /// must echo a current preview token; drift returns <c>409 invoice.preview_stale</c>,
    /// duplicates return <c>409 invoice.duplicate</c>).
    /// </summary>
    /// <param name="request">The create request including the preview token.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created invoice detail.</returns>
    public Task<ApiResponse<InvoiceDetailDto>> CreateInvoiceAsync(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateInvoiceRequest, InvoiceDetailDto>("api/invoices", request, cancellationToken);

    /// <summary>Body-based reconciliation search (Admin/Coordinator, D-S6-18; max 92-day window).</summary>
    /// <param name="request">The search filters + paging.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>One page of rows plus full-filter KPI totals.</returns>
    public Task<ApiResponse<BillingReconciliationSearchResponseDto>> SearchReconciliationAsync(
        BillingReconciliationSearchRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<BillingReconciliationSearchRequest, BillingReconciliationSearchResponseDto>(
            "api/invoices/reconciliation/search", request, cancellationToken);

    /// <summary>Guarded reconciliation drill-in for one shift (Admin/Coordinator).</summary>
    /// <param name="shiftId">Shift identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The row classification plus append-only resolution history.</returns>
    public Task<ApiResponse<BillingReconciliationDetailDto>> GetReconciliationDetailAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default) =>
        GetAsync<BillingReconciliationDetailDto>(
            $"api/invoices/reconciliation/shifts/{shiftId}", cancellationToken);

    /// <summary>Records an audited non-billable resolution (Admin/Coordinator; append-only).</summary>
    /// <param name="shiftId">Shift identifier.</param>
    /// <param name="request">The resolution. The note must be PHI-free.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated reconciliation detail.</returns>
    public Task<ApiResponse<BillingReconciliationDetailDto>> ResolveNonBillableAsync(
        Guid shiftId,
        ResolveNonBillableRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<ResolveNonBillableRequest, BillingReconciliationDetailDto>(
            $"api/invoices/reconciliation/shifts/{shiftId}/resolve", request, cancellationToken);

    /// <summary>Reopens a resolved service by appending a superseding record (Admin/Coordinator).</summary>
    /// <param name="shiftId">Shift identifier.</param>
    /// <param name="request">The reopen request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated reconciliation detail.</returns>
    public Task<ApiResponse<BillingReconciliationDetailDto>> ReopenResolutionAsync(
        Guid shiftId,
        ReopenResolutionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<ReopenResolutionRequest, BillingReconciliationDetailDto>(
            $"api/invoices/reconciliation/shifts/{shiftId}/reopen", request, cancellationToken);

    /// <summary>Dedicated audited missing-time correction (Admin/Coordinator, D-S6-18).</summary>
    /// <param name="shiftId">Shift identifier.</param>
    /// <param name="request">The corrected window, break, and safe reason code.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated reconciliation detail.</returns>
    public Task<ApiResponse<BillingReconciliationDetailDto>> CorrectShiftTimeAsync(
        Guid shiftId,
        CorrectShiftTimeRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CorrectShiftTimeRequest, BillingReconciliationDetailDto>(
            $"api/invoices/reconciliation/shifts/{shiftId}/correct-time", request, cancellationToken);

    /// <summary>Records a payment against an invoice (Admin/Coordinator).</summary>
    /// <param name="invoiceId">Invoice identifier.</param>
    /// <param name="request">The payment request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated invoice detail.</returns>
    public Task<ApiResponse<InvoiceDetailDto>> RecordPaymentAsync(
        Guid invoiceId,
        RecordPaymentRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<RecordPaymentRequest, InvoiceDetailDto>(
            $"api/invoices/{invoiceId}/payments", request, cancellationToken);

    /// <summary>Gets the period margin summary (Admin only).</summary>
    /// <param name="periodStartUtc">Period start (UTC, inclusive).</param>
    /// <param name="periodEndUtc">Period end (UTC, exclusive).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The margin summary.</returns>
    public Task<ApiResponse<MarginSummaryDto>> GetMarginSummaryAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default) =>
        GetAsync<MarginSummaryDto>(
            $"api/billing/margins?periodStartUtc={QueryFormat.Utc(periodStartUtc)}&periodEndUtc={QueryFormat.Utc(periodEndUtc)}",
            cancellationToken);

    /// <summary>Gets a page of per-shift margins for a period (Admin only).</summary>
    /// <param name="paging">Paging parameters.</param>
    /// <param name="periodStartUtc">Period start (UTC, inclusive).</param>
    /// <param name="periodEndUtc">Period end (UTC, exclusive).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The paged shift margins.</returns>
    public Task<ApiResponse<PagedResult<ShiftMarginDto>>> GetShiftMarginsAsync(
        PagedRequest paging,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<ShiftMarginDto>>(
            $"api/billing/margins/shifts?{paging.ToQueryString()}&periodStartUtc={QueryFormat.Utc(periodStartUtc)}&periodEndUtc={QueryFormat.Utc(periodEndUtc)}",
            cancellationToken);
}
