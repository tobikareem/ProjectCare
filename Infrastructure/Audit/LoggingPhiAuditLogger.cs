using CarePath.Application.Abstractions.Audit;
using Microsoft.Extensions.Logging;

namespace CarePath.Infrastructure.Audit;

/// <summary>
/// Writes PHI audit metadata without logging protected values.
/// </summary>
public sealed class LoggingPhiAuditLogger : IPhiAuditLogger
{
    private readonly ILogger<LoggingPhiAuditLogger> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingPhiAuditLogger"/> class.
    /// </summary>
    /// <param name="logger">Logger used for metadata-only audit entries.</param>
    public LoggingPhiAuditLogger(ILogger<LoggingPhiAuditLogger> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    public Task LogAsync(PhiAuditEntry entry, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "PHI audit {Action} {EntityType} {EntityId} actor {ActorUserId} actorType {ActorType} trace {TraceId} attributes {Attributes}",
            entry.Action,
            entry.EntityType,
            entry.EntityId,
            entry.ActorUserId,
            entry.ActorType,
            entry.CorrelationId,
            entry.Attributes);

        return Task.CompletedTask;
    }
}
