namespace CarePath.Infrastructure.Persistence;

/// <summary>
/// Supported EF Core database providers, selected at startup via the
/// <c>Database:Provider</c> configuration value.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>Microsoft SQL Server / Azure SQL. The default when configuration omits a provider.</summary>
    SqlServer,

    /// <summary>PostgreSQL via the Npgsql provider.</summary>
    PostgreSql,
}
