using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Scheduling.Services;
using CarePath.Contracts.Common;
using CarePath.Contracts.Scheduling;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarePath.WebApi.Controllers;

[ApiController]
[Route("api/shifts")]
public sealed class ShiftsController : ControllerBase
{
    private readonly IShiftOperationsService shiftService;
    private readonly IVisitDocumentationService visitDocumentationService;
    private readonly IIdorGuard idorGuard;

    public ShiftsController(IShiftOperationsService shiftService, IVisitDocumentationService visitDocumentationService, IIdorGuard idorGuard)
    {
        this.shiftService = shiftService;
        this.visitDocumentationService = visitDocumentationService;
        this.idorGuard = idorGuard;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Coordinator,Caregiver,Client,FacilityManager,Clinician")]
    public async Task<ActionResult<PagedResult<ShiftSummaryDto>>> GetShifts(
        [FromQuery] PagedRequest request,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
        => Ok(await shiftService.GetShiftsAsync(request, fromUtc, toUtc, cancellationToken));

    [HttpGet("coverage")]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<PagedResult<OpenShiftCoverageDto>>> GetCoverageQueue([FromQuery] PagedRequest request, CancellationToken cancellationToken)
        => Ok(await shiftService.GetCoverageQueueAsync(request, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Coordinator,Caregiver,Client,FacilityManager,Clinician")]
    public async Task<ActionResult<ShiftDetailDto>> GetShift(Guid id, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Shift, id, ObjectAccessAction.Read, cancellationToken);
        return Ok(await shiftService.GetShiftAsync(id, cancellationToken));
    }

    [HttpGet("{id:guid}/eligible-caregivers")]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<PagedResult<EligibleCaregiverDto>>> GetEligibleCaregivers(Guid id, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Shift, id, ObjectAccessAction.Read, cancellationToken);
        return Ok(await shiftService.GetEligibleCaregiversAsync(id, request, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<ShiftDetailDto>> CreateShift([FromBody] CreateShiftRequest request, CancellationToken cancellationToken)
    {
        var result = await shiftService.CreateShiftAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetShift), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<ShiftDetailDto>> UpdateShift(Guid id, [FromBody] UpdateShiftRequest request, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Shift, id, ObjectAccessAction.Update, cancellationToken);
        return Ok(await shiftService.UpdateShiftAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/check-in")]
    [Authorize(Roles = "Caregiver")]
    public async Task<ActionResult<ShiftDetailDto>> CheckIn(Guid id, [FromBody] CheckInRequest request, CancellationToken cancellationToken)
    {
        EnsureRouteMatchesBody(id, request.ShiftId);
        await EnsureAuthorizedAsync(ProtectedResourceType.Shift, id, ObjectAccessAction.Update, cancellationToken);
        return Ok(await shiftService.CheckInAsync(request, cancellationToken));
    }

    [HttpPost("{id:guid}/check-out")]
    [Authorize(Roles = "Caregiver")]
    public async Task<ActionResult<ShiftDetailDto>> CheckOut(Guid id, [FromBody] CheckOutRequest request, CancellationToken cancellationToken)
    {
        EnsureRouteMatchesBody(id, request.ShiftId);
        await EnsureAuthorizedAsync(ProtectedResourceType.Shift, id, ObjectAccessAction.Update, cancellationToken);
        return Ok(await shiftService.CheckOutAsync(request, cancellationToken));
    }

    [HttpGet("{shiftId:guid}/visit-notes")]
    [Authorize(Roles = "Admin,Coordinator,Clinician,Client,Caregiver")]
    public async Task<ActionResult<PagedResult<VisitNoteSummaryDto>>> GetVisitNotes(Guid shiftId, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Shift, shiftId, ObjectAccessAction.Read, cancellationToken);
        return Ok(await visitDocumentationService.GetVisitNotesAsync(shiftId, request, cancellationToken));
    }

    [HttpPost("{shiftId:guid}/visit-notes")]
    [Authorize(Roles = "Caregiver")]
    public async Task<ActionResult<VisitNoteDetailDto>> CreateVisitNote(Guid shiftId, [FromBody] CreateVisitNoteRequest request, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Shift, shiftId, ObjectAccessAction.Create, cancellationToken);
        return Ok(await visitDocumentationService.CreateVisitNoteAsync(shiftId, request, cancellationToken));
    }

    private async Task EnsureAuthorizedAsync(ProtectedResourceType resourceType, Guid resourceId, ObjectAccessAction action, CancellationToken cancellationToken)
    {
        var result = await idorGuard.EnsureAuthorizedAsync(resourceType, resourceId, action, cancellationToken);
        if (!result.IsAuthorized)
        {
            throw new ResourceAccessDeniedException(result.DenialCode ?? "ResourceUnavailable", isPhiResource: true);
        }
    }

    private static void EnsureRouteMatchesBody(Guid routeId, Guid bodyId)
    {
        if (routeId != bodyId)
        {
            throw new ValidationException("The request is invalid.");
        }
    }
}
