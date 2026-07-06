using CarePath.Application.Billing.Services;
using CarePath.Contracts.Billing;
using CarePath.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarePath.WebApi.Controllers;

[ApiController]
[Route("api/billing/margins")]
[Authorize(Roles = "Admin")]
public sealed class BillingMarginsController : ControllerBase
{
    private readonly IBillingOperationsService service;

    public BillingMarginsController(IBillingOperationsService service)
    {
        this.service = service;
    }

    [HttpGet]
    public async Task<ActionResult<MarginSummaryDto>> GetMarginSummary(
        [FromQuery] DateTime periodStartUtc,
        [FromQuery] DateTime periodEndUtc,
        CancellationToken cancellationToken)
        => Ok(await service.GetMarginSummaryAsync(periodStartUtc, periodEndUtc, cancellationToken));

    [HttpGet("shifts")]
    public async Task<ActionResult<PagedResult<ShiftMarginDto>>> GetShiftMargins(
        [FromQuery] PagedRequest request,
        [FromQuery] DateTime periodStartUtc,
        [FromQuery] DateTime periodEndUtc,
        CancellationToken cancellationToken)
        => Ok(await service.GetShiftMarginsAsync(request, periodStartUtc, periodEndUtc, cancellationToken));
}
