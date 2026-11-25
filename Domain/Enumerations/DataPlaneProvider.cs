namespace CarePath.Domain.Enumerations;

/// <summary>
/// Database engine hosting an organization's operational data plane. Mirrors the
/// Infrastructure provider selection without the Domain referencing Infrastructure.
/// </summary>
public enum DataPlaneProvider
{
    /// <summary>Microsoft SQL Server / Azure SQL.</summary>
    SqlServer = 1,

    /// <summary>PostgreSQL.</summary>
    PostgreSql = 2
}
