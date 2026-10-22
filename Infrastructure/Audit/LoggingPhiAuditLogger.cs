using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using Microsoft.Extensions.Logging;

namespace CarePath.Infrastructure.Audit;

/// <summary>
/// Writes PHI audit metadata without logging protected values.
/// </summary>
public sealed class LoggingPhiAuditLogger : IPhiAuditLogger
{
    private readonly ILogger<LoggingPhiAuditLogger> logger;
    private readonly ICurrentUserContext? currentUser;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingPhiAuditLogger"/> class.
    /// </summary>
    /// <param name="logger">Logger used for metadata-only audit entries.</param>
    /// <param name="currentUser">
    /// Caller context supplying the organization the request resolved to (CP-04); optional for
    /// background callers that have none.
    /// </param>
    public LoggingPhiAuditLogger(ILogger<LoggingPhiAuditLogger> logger, ICurrentUserContext? currentUser = null)
    {
        this.logger = logger;
        this.currentUser = currentUser;
    }

    /// <inheritdoc />
    public Task LogAsync(PhiAuditEntry entry, CancellationToken cancellationToken = default)
    {
        // Organization correlation (design §HIPAA): explicit on the entry, else the request's
        // resolved organization. Identifier only — never a name or host.
        var organizationId = entry.OrganizationId ?? currentUser?.OrganizationId;

        logger.LogInformation(
            "PHI audit {Action} {EntityType} {EntityId} actor {ActorUserId} actorType {ActorType} organization {OrganizationId} trace {TraceId} attributes {Attributes}",
            entry.Action,
            entry.EntityType,
            entry.EntityId,
            entry.ActorUserId,
            entry.ActorType,
            organizationId,
            entry.CorrelationId,
            entry.Attributes);

        return Task.CompletedTask;
    }
}
