namespace CarePath.Application.Abstractions.Billing;

public interface IPersistenceConflictDetector
{
    bool IsUniqueConstraintConflict(Exception exception, string constraintOrIndexName);
}
