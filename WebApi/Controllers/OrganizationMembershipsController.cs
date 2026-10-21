using CarePath.Application.Organization.Services;
using CarePath.Contracts.Common;
using CarePath.Contracts.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarePath.WebApi.Controllers;

/// <summary>
/// Organization-administrator membership management (CP-04 FR-016/FR-017). The organization is
/// always the one the request host resolved to; it is never taken from the request.
/// </summary>
[ApiController]
[Route("api/organization/memberships")]
[Authorize(Roles = "Admin")]
public sealed class OrganizationMembershipsController : ControllerBase
{
    private readonly IOrganizationMembershipService service;

    /// <summary>Initializes the controller.</summary>
    public OrganizationMembershipsController(IOrganizationMembershipService service)
    {
        this.service = service;
    }

    /// <summary>Lists memberships.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<MembershipDto>>> GetMemberships([FromQuery] PagedRequest request, CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(request, cancellationToken));

    /// <summary>Creates a membership for an existing or new platform user.</summary>
    [HttpPost]
    public async Task<ActionResult<MembershipDto>> CreateMembership([FromBody] CreateMembershipRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return Created($"/api/organization/memberships/{result.Id}", result);
    }

    /// <summary>Changes a membership's role.</summary>
    [HttpPut("{id:guid}/role")]
    public async Task<ActionResult<MembershipDto>> UpdateRole(Guid id, [FromBody] UpdateMembershipRoleRequest request, CancellationToken cancellationToken) =>
        Ok(await service.ChangeRoleAsync(id, request, cancellationToken));

    /// <summary>Activates or deactivates a membership.</summary>
    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<MembershipDto>> UpdateStatus(Guid id, [FromBody] UpdateMembershipStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await service.SetStatusAsync(id, request, cancellationToken));
}
