using CarePath.Application.Organization.Services;
using CarePath.Contracts.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarePath.WebApi.Controllers;

/// <summary>Anonymous branding for the organization the request host resolved to.</summary>
[ApiController]
[Route("api/organization/branding")]
public sealed class OrganizationBrandingController : ControllerBase
{
    private readonly IOrganizationBrandingQuery query;

    /// <summary>Initializes the controller.</summary>
    /// <param name="query">Branding query for the resolved host.</param>
    public OrganizationBrandingController(IOrganizationBrandingQuery query)
    {
        this.query = query;
    }

    /// <summary>Returns approved branding fields; 404 when the host does not resolve to a tenant.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(OrganizationBrandingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationBrandingDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await query.GetForCurrentHostAsync(cancellationToken));
    }
}
