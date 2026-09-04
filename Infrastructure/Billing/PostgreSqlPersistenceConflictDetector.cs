using CarePath.Application.Abstractions.Billing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CarePath.Infrastructure.Billing;

/// <summary>Detects PostgreSQL unique constraint conflicts without leaking provider exceptions into Application.</summary>
public sealed class PostgreSqlPersistenceConflictDetector : IPersistenceConflictDetector
{
    private const string UniqueViolation = "23505";

    /// <inheritdoc />
    public bool IsUniqueConstraintConflict(Exception exception, string constraintOrIndexName)
    {
        var postgresException = Unwrap(exception);
        return postgresException is not null
            && string.Equals(postgresException.SqlState, UniqueViolation, StringComparison.Ordinal)
            && IdentifiesConstraint(postgresException, constraintOrIndexName);
    }

    // PostgreSQL reports the violated constraint as a structured field, which is more reliable
    // than searching message text; the message is only a fallback when the field is absent.
    private static bool IdentifiesConstraint(PostgresException exception, string constraintOrIndexName)
    {
        return string.Equals(exception.ConstraintName, constraintOrIndexName, StringComparison.Ordinal)
            || (string.IsNullOrEmpty(exception.ConstraintName)
                && exception.Message.Contains(constraintOrIndexName, StringComparison.Ordinal));
    }

    private static PostgresException? Unwrap(Exception exception)
    {
        return exception switch
        {
            DbUpdateException { InnerException: PostgresException postgresException } => postgresException,
            PostgresException postgresException => postgresException,
            { InnerException: not null } => Unwrap(exception.InnerException),
            _ => null,
        };
    }
}
