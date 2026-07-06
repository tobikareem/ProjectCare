using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Billing.Services;
using CarePath.Application.Common.Exceptions;
using CarePath.Contracts.Billing;
using CarePath.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarePath.WebApi.Controllers;

[ApiController]
[Route("api/invoices")]
public sealed class InvoicesController : ControllerBase
{
    private readonly IBillingOperationsService service;
    private readonly IIdorGuard idorGuard;

    public InvoicesController(IBillingOperationsService service, IIdorGuard idorGuard)
    {
        this.service = service;
        this.idorGuard = idorGuard;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<PagedResult<InvoiceSummaryDto>>> GetInvoices([FromQuery] PagedRequest request, CancellationToken cancellationToken)
        => Ok(await service.GetInvoicesAsync(request, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Coordinator,Client")]
    public async Task<ActionResult<InvoiceDetailDto>> GetInvoice(Guid id, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Invoice, id, ObjectAccessAction.Read, cancellationToken);
        return Ok(await service.GetInvoiceAsync(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<InvoiceDetailDto>> CreateInvoice([FromBody] CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateInvoiceAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetInvoice), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/payments")]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<InvoiceDetailDto>> RecordPayment(Guid id, [FromBody] RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Invoice, id, ObjectAccessAction.Update, cancellationToken);
        return Ok(await service.RecordPaymentAsync(id, request, cancellationToken));
    }

    private async Task EnsureAuthorizedAsync(ProtectedResourceType resourceType, Guid resourceId, ObjectAccessAction action, CancellationToken cancellationToken)
    {
        var result = await idorGuard.EnsureAuthorizedAsync(resourceType, resourceId, action, cancellationToken);
        if (!result.IsAuthorized)
        {
            throw new ResourceAccessDeniedException(result.DenialCode ?? "ResourceUnavailable", isPhiResource: true);
        }
    }
}
