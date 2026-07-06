namespace CarePath.Application.Abstractions.Billing;

public sealed class NoOpPersistenceConflictDetector : IPersistenceConflictDetector
{
    public bool IsUniqueConstraintConflict(Exception exception, string constraintOrIndexName) => false;
}
