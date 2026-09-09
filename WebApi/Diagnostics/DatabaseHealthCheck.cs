using CarePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CarePath.WebApi.Diagnostics;

/// <summary>
/// Readiness check confirming the configured database is reachable. It uses whichever EF Core
/// provider <c>Database:Provider</c> selected, so the same check serves SQL Server/Azure SQL and
/// PostgreSQL without provider-specific logic.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly CarePathDbContext dbContext;

    /// <summary>Initializes the check over the CarePath database context.</summary>
    /// <param name="dbContext">CarePath EF Core context.</param>
    public DatabaseHealthCheck(CarePathDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Connection-only probe: opens a connection and closes it. Reads no rows and touches
        // no schema, so it stays cheap enough to run on a container health-check interval.
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy("Database reachable.")
            : HealthCheckResult.Unhealthy("Database unreachable.");
    }
}
