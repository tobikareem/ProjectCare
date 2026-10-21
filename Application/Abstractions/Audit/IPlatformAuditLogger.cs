namespace CarePath.Application.Abstractions.Audit;

/// <summary>
/// Append-only writer for the platform audit log. The only code path that creates
/// <c>PlatformAuditEvent</c> rows; there is no update or delete path.
/// </summary>
public interface IPlatformAuditLogger
{
    /// <summary>Appends one audit event.</summary>
    /// <param name="entry">Non-PHI entry to record.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    Task LogAsync(PlatformAuditEntry entry, CancellationToken cancellationToken = default);
}
