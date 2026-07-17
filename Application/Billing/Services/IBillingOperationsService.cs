using CarePath.Contracts.Billing;
using CarePath.Contracts.Common;

namespace CarePath.Application.Billing.Services;

public interface IBillingOperationsService
{
    Task<InvoicePreviewResponseDto> PreviewInvoiceAsync(InvoicePreviewRequest request, CancellationToken cancellationToken = default);

    Task<InvoiceDetailDto> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default);

    Task<InvoiceDetailDto> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest request, CancellationToken cancellationToken = default);

    Task<InvoiceDetailDto> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    Task<PagedResult<InvoiceSummaryDto>> GetInvoicesAsync(PagedRequest request, CancellationToken cancellationToken = default);

    Task<MarginSummaryDto> GetMarginSummaryAsync(DateTime periodStartUtc, DateTime periodEndUtc, CancellationToken cancellationToken = default);

    Task<PagedResult<ShiftMarginDto>> GetShiftMarginsAsync(PagedRequest request, DateTime periodStartUtc, DateTime periodEndUtc, CancellationToken cancellationToken = default);
}
