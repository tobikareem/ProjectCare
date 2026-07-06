using CarePath.Application.Abstractions.Billing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.Billing;

/// <summary>Detects SQL Server unique constraint conflicts without leaking provider exceptions into Application.</summary>
public sealed class SqlServerPersistenceConflictDetector : IPersistenceConflictDetector
{
    private const int UniqueConstraintViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    /// <inheritdoc />
    public bool IsUniqueConstraintConflict(Exception exception, string constraintOrIndexName)
    {
        var sqlException = Unwrap(exception);
        return sqlException?.Errors.Cast<SqlError>().Any(error =>
            (error.Number == UniqueConstraintViolation || error.Number == UniqueIndexViolation)
            && error.Message.Contains(constraintOrIndexName, StringComparison.Ordinal)) == true;
    }

    private static SqlException? Unwrap(Exception exception)
    {
        return exception switch
        {
            DbUpdateException { InnerException: SqlException sqlException } => sqlException,
            SqlException sqlException => sqlException,
            { InnerException: not null } => Unwrap(exception.InnerException),
            _ => null,
        };
    }
}
