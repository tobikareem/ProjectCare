using CarePath.Infrastructure.ControlPlane.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CarePath.WebApi.Diagnostics;

/// <summary>
/// Readiness check confirming the control-plane database is reachable. Host resolution fails
/// closed without it, so an instance that cannot reach the control plane must not receive traffic.
/// </summary>
public sealed class ControlPlaneHealthCheck : IHealthCheck
{
    private readonly ControlPlaneDbContext dbContext;

    /// <summary>Initializes the check over the control-plane context.</summary>
    /// <param name="dbContext">Control-plane EF Core context.</param>
    public ControlPlaneHealthCheck(ControlPlaneDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy("Control plane reachable.")
            : HealthCheckResult.Unhealthy("Control plane unreachable.");
    }
}
