namespace CarePath.Application.Abstractions.Audit;

public interface IPhiAuditLogger
{
    Task LogAsync(
        PhiAuditEntry entry,
        CancellationToken cancellationToken = default);
}