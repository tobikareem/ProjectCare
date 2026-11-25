using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarePath.Application.Tests.Integration;

/// <summary>
/// Test-only tenant endpoint that records whether a request got past host resolution and
/// authorization. Proves that a rejected request never invokes a controller action.
/// </summary>
[ApiController]
[Route("api/integration/probe")]
[Authorize]
public sealed class ProbeController : ControllerBase
{
    private readonly ControlPlaneTestHost.ProbeCounter _counter;

    /// <summary>Initializes the controller.</summary>
    public ProbeController(ControlPlaneTestHost.ProbeCounter counter)
    {
        _counter = counter;
    }

    /// <summary>Counts and returns the caller's organization claim.</summary>
    [HttpGet]
    public IActionResult Get()
    {
        _counter.Increment();
        return Ok(new { organizationId = User.FindFirst("organization_id")?.Value });
    }
}
