using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Clients.Services;
using CarePath.Application.Common.Exceptions;
using CarePath.Contracts.Clients;
using CarePath.Contracts.Common;
using CarePath.Contracts.Scheduling;
using CarePath.Application.Scheduling.Services;
using CarePath.WebApi.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarePath.WebApi.Controllers;

[ApiController]
[Route("api/clients")]
public sealed class ClientsController : ControllerBase
{
    private const string ResourceNotFoundCode = "resource.not_found";

    private readonly IClientOperationsService clientService;
    private readonly IClientAccessGrantService grantService;
    private readonly IIdorGuard idorGuard;
    private readonly IAssignmentHistoryService assignmentHistoryService;

    public ClientsController(IClientOperationsService clientService, IClientAccessGrantService grantService, IIdorGuard idorGuard, IAssignmentHistoryService assignmentHistoryService)
    {
        this.clientService = clientService;
        this.grantService = grantService;
        this.idorGuard = idorGuard;
        this.assignmentHistoryService = assignmentHistoryService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Coordinator,Clinician")]
    public async Task<ActionResult<PagedResult<ClientSummaryDto>>> GetClients([FromQuery] PagedRequest request, CancellationToken cancellationToken)
        => Ok(await clientService.GetClientsAsync(request, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Coordinator,Clinician,Client,Caregiver")]
    public async Task<ActionResult<ClientDetailDto>> GetClient(Guid id, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Client, id, ObjectAccessAction.Read, cancellationToken);
        var client = await clientService.GetClientAsync(id, cancellationToken);
        return client is null ? ClientNotFound() : Ok(client);
    }

    [HttpPost("{id:guid}/caregiver-assignments/search")]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<PagedResult<CaregiverAssignmentSummaryDto>>> SearchCaregiverAssignments(
        Guid id,
        [FromBody] AssignmentHistorySearchRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Client, id, ObjectAccessAction.Read, cancellationToken);
        return Ok(await assignmentHistoryService.GetCaregiversForClientAsync(id, request, cancellationToken));
    }

    [HttpPost("me/caregiver-assignments/search")]
    [Authorize(Roles = "Client")]
    public async Task<ActionResult<PagedResult<MyCaregiverAssignmentSummaryDto>>> SearchMyCaregivers(
        [FromBody] AssignmentHistorySearchRequest request,
        CancellationToken cancellationToken) =>
        Ok(await assignmentHistoryService.GetMyCaregiversAsync(request, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<ClientDetailDto>> CreateClient([FromBody] CreateClientRequest request, CancellationToken cancellationToken)
    {
        var result = await clientService.CreateClientAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetClient), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<ClientDetailDto>> UpdateClient(Guid id, [FromBody] UpdateClientRequest request, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Client, id, ObjectAccessAction.Update, cancellationToken);
        return Ok(await clientService.UpdateClientAsync(id, request, cancellationToken));
    }

    [HttpGet("{clientId:guid}/care-plans")]
    [Authorize(Roles = "Admin,Coordinator,Clinician,Client,Caregiver")]
    public async Task<ActionResult<PagedResult<CarePlanDto>>> GetCarePlans(Guid clientId, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Client, clientId, ObjectAccessAction.Read, cancellationToken);
        return Ok(await clientService.GetCarePlansAsync(clientId, request, cancellationToken));
    }

    [HttpPost("{clientId:guid}/care-plans")]
    [Authorize(Roles = "Admin,Coordinator,Clinician")]
    public async Task<ActionResult<CarePlanDto>> CreateCarePlan(Guid clientId, [FromBody] CreateCarePlanRequest request, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Client, clientId, ObjectAccessAction.Create, cancellationToken);
        return Ok(await clientService.CreateCarePlanAsync(clientId, request, cancellationToken));
    }

    [HttpGet("{clientId:guid}/access-grants")]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<IReadOnlyList<ClientAccessGrantDto>>> GetAccessGrants(Guid clientId, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Client, clientId, ObjectAccessAction.Read, cancellationToken);
        return Ok(await grantService.GetGrantsAsync(clientId, cancellationToken));
    }

    [HttpPost("{clientId:guid}/access-grants")]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<ActionResult<ClientAccessGrantDto>> CreateAccessGrant(Guid clientId, [FromBody] CreateGrantRequest request, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Client, clientId, ObjectAccessAction.Create, cancellationToken);
        return Ok(await grantService.CreateGrantAsync(clientId, request, cancellationToken));
    }

    [HttpDelete("{clientId:guid}/access-grants/{grantId:guid}")]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<IActionResult> RevokeAccessGrant(Guid clientId, Guid grantId, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.Client, clientId, ObjectAccessAction.Delete, cancellationToken);
        await grantService.RevokeGrantAsync(clientId, grantId, cancellationToken);
        return NoContent();
    }

    private async Task EnsureAuthorizedAsync(ProtectedResourceType resourceType, Guid resourceId, ObjectAccessAction action, CancellationToken cancellationToken)
    {
        var result = await idorGuard.EnsureAuthorizedAsync(resourceType, resourceId, action, cancellationToken);
        if (!result.IsAuthorized)
        {
            throw new ResourceAccessDeniedException(result.DenialCode ?? "ResourceUnavailable", isPhiResource: true);
        }
    }

    private static NotFoundObjectResult ClientNotFound()
    {
        return new NotFoundObjectResult(new ProblemDetailsResponse
        {
            Type = "about:blank",
            Title = "Resource not found.",
            Status = StatusCodes.Status404NotFound,
            Detail = null,
            Instance = null,
            TraceId = null,
            ValidationErrors = [],
            Errors = [new ApiError(ResourceNotFoundCode, "Resource not found.")],
        });
    }
}
