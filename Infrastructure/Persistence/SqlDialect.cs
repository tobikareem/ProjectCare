namespace CarePath.Infrastructure.Persistence;

/// <summary>
/// Provider-specific SQL fragments for the few places the model must emit raw SQL,
/// currently filtered index expressions. Resolved once per model build from the active
/// EF Core provider so entity configurations stay free of provider checks.
/// </summary>
public sealed class SqlDialect
{
    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    private readonly bool _isPostgreSql;

    private SqlDialect(bool isPostgreSql)
    {
        _isPostgreSql = isPostgreSql;
    }

    /// <summary>Creates the dialect for the named EF Core provider.</summary>
    /// <param name="providerName">
    /// The active provider name, normally <c>DatabaseFacade.ProviderName</c>. Anything other
    /// than Npgsql uses SQL Server syntax, which is also valid for the SQLite test provider.
    /// </param>
    /// <returns>A dialect that emits SQL for that provider.</returns>
    public static SqlDialect For(string? providerName)
    {
        return new SqlDialect(string.Equals(providerName, NpgsqlProviderName, StringComparison.Ordinal));
    }

    /// <summary>Quotes an identifier using the provider's delimiter.</summary>
    /// <param name="identifier">Unquoted column or table name.</param>
    /// <returns>The identifier wrapped in the provider's quoting characters.</returns>
    public string Quote(string identifier)
    {
        return _isPostgreSql ? $"\"{identifier}\"" : $"[{identifier}]";
    }

    /// <summary>Builds a "column IS NOT NULL" filtered-index predicate.</summary>
    /// <param name="columnName">Unquoted column name.</param>
    /// <returns>Provider-valid SQL for the predicate.</returns>
    public string IsNotNull(string columnName)
    {
        return $"{Quote(columnName)} IS NOT NULL";
    }

    /// <summary>
    /// Builds a "column is false" filtered-index predicate. PostgreSQL compares against the
    /// boolean literal; SQL Server stores <c>bit</c> and has no boolean literal, so it compares
    /// against 0.
    /// </summary>
    /// <param name="columnName">Unquoted column name of a boolean/bit column.</param>
    /// <returns>Provider-valid SQL for the predicate.</returns>
    public string IsFalse(string columnName)
    {
        return _isPostgreSql ? $"{Quote(columnName)} = false" : $"{Quote(columnName)} = 0";
    }
}
