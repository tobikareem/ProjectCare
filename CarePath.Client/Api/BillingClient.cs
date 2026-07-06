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

    /// <summary>Creates a period invoice (Admin/Coordinator; idempotent per D-S4-6 — duplicates return a conflict).</summary>
    /// <param name="request">The create request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created invoice detail.</returns>
    public Task<ApiResponse<InvoiceDetailDto>> CreateInvoiceAsync(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateInvoiceRequest, InvoiceDetailDto>("api/invoices", request, cancellationToken);

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
