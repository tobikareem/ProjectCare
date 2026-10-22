using CarePath.Application.Platform.Services;
using CarePath.Contracts.Common;
using CarePath.Contracts.Platform;
using CarePath.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarePath.WebApi.Controllers;

/// <summary>
/// Platform-operator organization management (CP-04 FR-014). Reachable only with a platform-kind
/// token at the platform host: a tenant token here is 403 at its own host and 401 elsewhere.
/// </summary>
[ApiController]
[Route("api/platform/organizations")]
[Authorize(Policy = CarePathAuthorizationPolicies.PlatformAdmin)]
public sealed class PlatformOrganizationsController : ControllerBase
{
    private readonly IPlatformOrganizationService service;

    /// <summary>Initializes the controller.</summary>
    public PlatformOrganizationsController(IPlatformOrganizationService service)
    {
        this.service = service;
    }

    /// <summary>Lists organizations.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<OrganizationSummaryDto>>> GetOrganizations([FromQuery] PagedRequest request, CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(request, cancellationToken));

    /// <summary>Registers an organization (Pending / Registered).</summary>
    [HttpPost]
    public async Task<ActionResult<OrganizationDto>> RegisterOrganization([FromBody] RegisterOrganizationRequest request, CancellationToken cancellationToken)
    {
        var result = await service.RegisterAsync(request, cancellationToken);
        return Created($"/api/platform/organizations/{result.Id}", result);
    }

    /// <summary>Gets an organization.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrganizationDto>> GetOrganization(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(id, cancellationToken));

    /// <summary>Updates names, time zone, and region.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrganizationDto>> UpdateOrganization(Guid id, [FromBody] UpdateOrganizationRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(id, request, cancellationToken));

    /// <summary>Suspends an organization.</summary>
    [HttpPost("{id:guid}/suspend")]
    public async Task<ActionResult<OrganizationDto>> SuspendOrganization(Guid id, [FromBody] SuspendOrganizationRequest request, CancellationToken cancellationToken) =>
        Ok(await service.SuspendAsync(id, request, cancellationToken));

    /// <summary>Reactivates a suspended, provisioned organization.</summary>
    [HttpPost("{id:guid}/reactivate")]
    public async Task<ActionResult<OrganizationDto>> ReactivateOrganization(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.ReactivateAsync(id, cancellationToken));

    /// <summary>Lists host names.</summary>
    [HttpGet("{id:guid}/domains")]
    public async Task<ActionResult<IReadOnlyList<OrganizationDomainDto>>> GetDomains(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetDomainsAsync(id, cancellationToken));

    /// <summary>Adds a host name.</summary>
    [HttpPost("{id:guid}/domains")]
    public async Task<ActionResult<OrganizationDomainDto>> AddDomain(Guid id, [FromBody] AddOrganizationDomainRequest request, CancellationToken cancellationToken)
    {
        var result = await service.AddDomainAsync(id, request, cancellationToken);
        return Created($"/api/platform/organizations/{id}/domains/{result.Id}", result);
    }

    /// <summary>Removes a host name.</summary>
    [HttpDelete("{id:guid}/domains/{domainId:guid}")]
    public async Task<IActionResult> RemoveDomain(Guid id, Guid domainId, CancellationToken cancellationToken)
    {
        await service.RemoveDomainAsync(id, domainId, cancellationToken);
        return NoContent();
    }

    /// <summary>Gets branding.</summary>
    [HttpGet("{id:guid}/branding")]
    public async Task<ActionResult<OrganizationBrandingDetailDto>> GetBranding(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetBrandingAsync(id, cancellationToken));

    /// <summary>Replaces branding.</summary>
    [HttpPut("{id:guid}/branding")]
    public async Task<ActionResult<OrganizationBrandingDetailDto>> UpdateBranding(Guid id, [FromBody] UpdateOrganizationBrandingRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateBrandingAsync(id, request, cancellationToken));
}
