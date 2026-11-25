using System.Collections.Concurrent;

namespace CarePath.Infrastructure.ControlPlane.Audit;

/// <summary>
/// Per-instance throttle so a scanner cycling random <c>Host</c> headers cannot grow the
/// platform audit table one row per request (D-CP04-06). Keyed by host hash, never by host.
/// Exactly one caller per hash wins each window, even under contention.
/// </summary>
public sealed class HostDenialAuditThrottle
{
    private const int MaxTrackedHashes = 50_000;
    private const int PurgeSampleMask = 0x3FF;

    /// <summary>Hard cap on audit rows written per window per instance, whatever the host mix.</summary>
    public const int MaxWritesPerWindow = 300;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastWritten = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly object _globalGate = new();
    private int _calls;
    private DateTimeOffset _globalWindowStart;
    private int _globalWrites;

    /// <summary>Initializes the throttle.</summary>
    /// <param name="timeProvider">Clock, injectable for tests.</param>
    public HostDenialAuditThrottle(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Returns true, and records the write, when no event for <paramref name="hostHash"/> has been
    /// written within <paramref name="window"/>; otherwise false. When the tracked set is at
    /// capacity, unknown hashes are refused rather than tracked, bounding memory under a flood.
    /// </summary>
    /// <param name="hostHash">SHA-256 hex of the normalized host.</param>
    /// <param name="window">Minimum interval between events for the same hash.</param>
    public bool ShouldWrite(string hostHash, TimeSpan window)
    {
        var now = _timeProvider.GetUtcNow();
        MaybePurge(now, window);

        // Per-hash first: a flood of one repeated host must not consume the global budget and
        // silence denials from every other host for the rest of the window.
        if (_lastWritten.TryGetValue(hostHash, out var recent) && now - recent < window)
        {
            return false;
        }

        // Rotating host names defeats the per-hash key, so a global cap bounds the write rate too.
        if (!TryConsumeGlobalBudget(now, window))
        {
            return false;
        }

        while (true)
        {
            if (_lastWritten.TryGetValue(hostHash, out var last))
            {
                if (now - last < window)
                {
                    return false;
                }

                if (_lastWritten.TryUpdate(hostHash, now, last))
                {
                    return true;
                }

                continue;
            }

            if (_lastWritten.Count >= MaxTrackedHashes)
            {
                return false;
            }

            if (_lastWritten.TryAdd(hostHash, now))
            {
                return true;
            }
        }
    }

    private bool TryConsumeGlobalBudget(DateTimeOffset now, TimeSpan window)
    {
        if (window == TimeSpan.Zero)
        {
            return true;
        }

        lock (_globalGate)
        {
            if (now - _globalWindowStart >= window)
            {
                _globalWindowStart = now;
                _globalWrites = 0;
            }

            if (_globalWrites >= MaxWritesPerWindow)
            {
                return false;
            }

            _globalWrites++;
            return true;
        }
    }

    // Sampled, not per-call: a full scan on every request would hand an attacker a CPU
    // amplification vector on exactly the path this type protects.
    private void MaybePurge(DateTimeOffset now, TimeSpan window)
    {
        if ((Interlocked.Increment(ref _calls) & PurgeSampleMask) != 0)
        {
            return;
        }

        foreach (var pair in _lastWritten)
        {
            if (now - pair.Value >= window)
            {
                _lastWritten.TryRemove(new KeyValuePair<string, DateTimeOffset>(pair.Key, pair.Value));
            }
        }
    }
}
