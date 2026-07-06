using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Clients.Services;
using CarePath.Application.Common.Exceptions;
using CarePath.Contracts.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarePath.WebApi.Controllers;

[ApiController]
[Route("api/care-plans")]
public sealed class CarePlansController : ControllerBase
{
    private readonly IClientOperationsService service;
    private readonly IIdorGuard idorGuard;

    public CarePlansController(IClientOperationsService service, IIdorGuard idorGuard)
    {
        this.service = service;
        this.idorGuard = idorGuard;
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Coordinator,Clinician")]
    public async Task<ActionResult<CarePlanDto>> UpdateCarePlan(Guid id, [FromBody] UpdateCarePlanRequest request, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.CarePlan, id, ObjectAccessAction.Update, cancellationToken);
        return Ok(await service.UpdateCarePlanAsync(id, request, cancellationToken));
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
