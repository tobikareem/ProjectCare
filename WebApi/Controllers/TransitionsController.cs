using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Transitions.Services;
using CarePath.Contracts.Common;
using CarePath.Contracts.Transitions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarePath.WebApi.Controllers;

[ApiController]
[Route("api/transitions")]
public sealed class TransitionsController : ControllerBase
{
    private readonly ITransitionsService service;
    private readonly IIdorGuard idorGuard;

    public TransitionsController(ITransitionsService service, IIdorGuard idorGuard)
    {
        this.service = service;
        this.idorGuard = idorGuard;
    }

    [HttpPost("documents")]
    [Authorize(Roles = "Coordinator")]
    public async Task<ActionResult<DischargeDocumentDto>> CreateDischargeDocument(
        [FromBody] CreateDischargeDocumentRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Client, request.ClientId, ObjectAccessAction.Create, cancellationToken);
        var result = await service.CreateDischargeDocumentAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDischargeDocument), new { id = result.Id }, result);
    }

    [HttpGet("documents/{id:guid}")]
    [Authorize(Roles = "Coordinator,Clinician")]
    public async Task<ActionResult<DischargeDocumentDto>> GetDischargeDocument(Guid id, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.DischargeDocument, id, ObjectAccessAction.Read, cancellationToken);
        return Ok(await service.GetDischargeDocumentAsync(id, cancellationToken));
    }

    [HttpGet("documents/{id:guid}/content")]
    [Authorize(Roles = "Coordinator,Clinician")]
    public async Task<ActionResult<DischargeDocumentContentDto>> GetDischargeDocumentContent(Guid id, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.DischargeDocument, id, ObjectAccessAction.Read, cancellationToken);
        return Ok(await service.GetDischargeDocumentContentAsync(id, cancellationToken));
    }

    [HttpPost("documents/{id:guid}/extract")]
    [Authorize(Roles = "Coordinator")]
    public async Task<ActionResult<TransitionPlanClinicalDto>> ExtractDischargeDocument(Guid id, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.DischargeDocument, id, ObjectAccessAction.Update, cancellationToken);
        var result = await service.ExtractDischargeDocumentAsync(id, cancellationToken);
        return AcceptedAtAction(nameof(GetPlan), new { id = result.Id }, result);
    }

    [HttpGet("plans")]
    [Authorize(Roles = "Coordinator,Clinician")]
    public async Task<ActionResult<PagedResult<TransitionPlanSummaryDto>>> GetPlans(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
        => Ok(await service.GetPlansAsync(request, cancellationToken));

    [HttpGet("plans/{id:guid}")]
    [Authorize(Roles = "Coordinator,Clinician")]
    public async Task<ActionResult<TransitionPlanClinicalDto>> GetPlan(Guid id, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.TransitionPlan, id, ObjectAccessAction.Read, cancellationToken);
        return Ok(await service.GetPlanAsync(id, cancellationToken));
    }

    [HttpGet("plans/{id:guid}/patient-view")]
    [Authorize(Roles = "Client")]
    public async Task<ActionResult<TransitionPlanPatientFacingDto>> GetPatientPlan(Guid id, CancellationToken cancellationToken)
        => Ok(await service.GetPatientPlanAsync(id, cancellationToken));

    [HttpPut("plans/{id:guid}/instructions/{instructionId:guid}")]
    [Authorize(Roles = "Clinician")]
    public async Task<ActionResult<TransitionInstructionClinicalDto>> ReviewInstruction(
        Guid id,
        Guid instructionId,
        [FromBody] ReviewInstructionRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.TransitionPlan, id, ObjectAccessAction.Update, cancellationToken);
        await EnsureAuthorizedAsync(ProtectedResourceType.TransitionInstruction, instructionId, ObjectAccessAction.Update, cancellationToken);
        return Ok(await service.ReviewInstructionAsync(id, instructionId, request, cancellationToken));
    }

    [HttpPost("plans/{id:guid}/activate")]
    [Authorize(Roles = "Clinician")]
    public async Task<ActionResult<TransitionPlanClinicalDto>> ActivatePlan(
        Guid id,
        [FromBody] ActivatePlanRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.TransitionPlan, id, ObjectAccessAction.Update, cancellationToken);
        return Ok(await service.ActivatePlanAsync(id, request, cancellationToken));
    }

    [HttpPost("plans/{id:guid}/reminders")]
    [Authorize(Roles = "Coordinator")]
    public async Task<ActionResult<TransitionReminderDto>> ScheduleReminder(
        Guid id,
        [FromBody] ScheduleReminderRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.TransitionPlan, id, ObjectAccessAction.Create, cancellationToken);
        return Ok(await service.ScheduleReminderAsync(id, request, cancellationToken));
    }

    [HttpPost("plans/{id:guid}/check-ins")]
    [Authorize(Roles = "Client")]
    public async Task<ActionResult<TransitionCheckInDto>> CreateCheckIn(
        Guid id,
        [FromBody] CreateCheckInRequest request,
        CancellationToken cancellationToken)
        => Ok(await service.CreateCheckInAsync(id, request, cancellationToken));

    [HttpGet("plans/{id:guid}/escalations")]
    [Authorize(Roles = "Coordinator")]
    public async Task<ActionResult<IReadOnlyList<TransitionEscalationDto>>> GetEscalations(Guid id, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.TransitionPlan, id, ObjectAccessAction.Read, cancellationToken);
        return Ok(await service.GetEscalationsAsync(id, cancellationToken));
    }

    [HttpPost("escalations/{id:guid}/acknowledge")]
    [Authorize(Roles = "Coordinator")]
    public async Task<ActionResult<TransitionEscalationDto>> AcknowledgeEscalation(
        Guid id,
        [FromBody] AcknowledgeEscalationRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.TransitionEscalation, id, ObjectAccessAction.Update, cancellationToken);
        return Ok(await service.AcknowledgeEscalationAsync(id, request, cancellationToken));
    }

    [HttpGet("plans/client/{clientId:guid}")]
    [Authorize(Roles = "Coordinator,Clinician,Caregiver")]
    public async Task<ActionResult<object>> GetPlanForClient(Guid clientId, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Client, clientId, ObjectAccessAction.Read, cancellationToken);
        return Ok(await service.GetPlanForClientAsync(clientId, cancellationToken));
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
