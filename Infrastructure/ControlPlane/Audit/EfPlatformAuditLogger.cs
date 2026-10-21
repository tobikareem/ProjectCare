using CarePath.Application.Abstractions.Audit;
using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.ControlPlane.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CarePath.Infrastructure.ControlPlane.Audit;

/// <summary>
/// Appends <see cref="PlatformAuditEvent"/> rows. The only writer of that table; it never updates
/// or deletes (the context rejects any non-insert on audit rows). Each write uses its own
/// context instance so an audit row never joins, or is rolled back with, a business transaction.
/// Host-denial events are throttled per host hash (D-CP04-06).
/// </summary>
public sealed class EfPlatformAuditLogger : IPlatformAuditLogger
{
    private const int MaxEntityTypeLength = 64;
    private const int MaxCorrelationIdLength = 64;
    private const int MaxHostHashLength = 64;

    private readonly IDbContextFactory<ControlPlaneDbContext> _contextFactory;
    private readonly HostDenialAuditThrottle _throttle;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _hostDenialWindow;

    /// <summary>Initializes the logger.</summary>
    /// <param name="contextFactory">Factory producing an isolated control-plane context per write.</param>
    /// <param name="throttle">Per-instance host-denial throttle.</param>
    /// <param name="timeProvider">Clock.</param>
    /// <param name="options">Control-plane options (audit window).</param>
    public EfPlatformAuditLogger(
        IDbContextFactory<ControlPlaneDbContext> contextFactory,
        HostDenialAuditThrottle throttle,
        TimeProvider timeProvider,
        IOptions<ControlPlaneOptions> options)
    {
        _contextFactory = contextFactory;
        _throttle = throttle;
        _timeProvider = timeProvider;
        _hostDenialWindow = TimeSpan.FromSeconds(Math.Max(0, options.Value.HostDenialAuditWindowSeconds));
    }

    /// <inheritdoc />
    public async Task LogAsync(PlatformAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var hostHash = Truncate(entry.HostHash, MaxHostHashLength);
        if (entry.Action == PlatformAuditAction.HostResolutionDenied
            && hostHash is not null
            && !_throttle.ShouldWrite(hostHash, _hostDenialWindow))
        {
            return;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.PlatformAuditEvents.Add(new PlatformAuditEvent
        {
            ActorPlatformUserId = entry.ActorPlatformUserId,
            OrganizationId = entry.OrganizationId,
            Action = entry.Action,
            EntityType = Truncate(entry.EntityType, MaxEntityTypeLength) ?? string.Empty,
            EntityId = entry.EntityId,
            CorrelationId = Truncate(entry.CorrelationId, MaxCorrelationIdLength),
            HostHash = hostHash,
            OccurredAtUtc = _timeProvider.GetUtcNow().UtcDateTime
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    // Column bounds are enforced here so an oversized caller value can never turn an anonymous
    // denied-host request into a DbUpdateException on the request path.
    private static string? Truncate(string? value, int maxLength) =>
        value is null ? null : value.Length <= maxLength ? value : value[..maxLength];
}
