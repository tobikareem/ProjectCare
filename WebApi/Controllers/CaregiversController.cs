using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Identity.Services;
using CarePath.Contracts.Common;
using CarePath.Contracts.Identity;
using CarePath.Contracts.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarePath.WebApi.Controllers;

[ApiController]
[Route("api/caregivers")]
public sealed class CaregiversController : ControllerBase
{
    private readonly ICaregiverOperationsService service;
    private readonly IIdorGuard idorGuard;

    public CaregiversController(ICaregiverOperationsService service, IIdorGuard idorGuard)
    {
        this.service = service;
        this.idorGuard = idorGuard;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<PagedResult<CaregiverSummaryDto>>> GetCaregivers([FromQuery] PagedRequest request, CancellationToken cancellationToken)
        => Ok(await service.GetCaregiversAsync(request, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Coordinator,Caregiver")]
    public async Task<ActionResult<CaregiverDetailDto>> GetCaregiver(Guid id, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Caregiver, id, ObjectAccessAction.Read, cancellationToken);
        return Ok(await service.GetCaregiverAsync(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<CaregiverDetailDto>> CreateCaregiver([FromBody] CreateCaregiverRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateCaregiverAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCaregiver), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<CaregiverDetailDto>> UpdateCaregiver(Guid id, [FromBody] UpdateCaregiverRequest request, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Caregiver, id, ObjectAccessAction.Update, cancellationToken);
        return Ok(await service.UpdateCaregiverAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/certifications")]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<CertificationDto>> AddCertification(Guid id, [FromBody] AddCertificationRequest request, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Caregiver, id, ObjectAccessAction.Update, cancellationToken);
        return Ok(await service.AddCertificationAsync(id, request, cancellationToken));
    }

    [HttpGet("certifications/expiring")]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<PagedResult<CertificationDto>>> GetExpiringCertifications([FromQuery] PagedRequest request, CancellationToken cancellationToken)
        => Ok(await service.GetExpiringCertificationsAsync(request, cancellationToken));

    [HttpGet("{id:guid}/eligible-shifts")]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<PagedResult<EligibleOpenShiftDto>>> GetEligibleOpenShifts(Guid id, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Caregiver, id, ObjectAccessAction.Read, cancellationToken);
        return Ok(await service.GetEligibleOpenShiftsAsync(id, request, cancellationToken));
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
