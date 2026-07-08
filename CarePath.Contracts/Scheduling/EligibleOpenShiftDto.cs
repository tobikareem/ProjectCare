using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Scheduling;

/// <summary>
/// Open shift row matched against a selected caregiver. Assignment writes still use
/// <see cref="UpdateShiftRequest"/> against the shift endpoint.
/// </summary>
public class EligibleOpenShiftDto
{
    /// <summary>Open shift identifier.</summary>
    public Guid ShiftId { get; init; }

    /// <summary>Client identifier used by existing shift update requests.</summary>
    public Guid ClientId { get; init; }

    /// <summary>Minimum-necessary client display label for authorized schedule views.</summary>
    public string ClientDisplayName { get; init; } = string.Empty;

    /// <summary>Scheduled start (UTC).</summary>
    public DateTime ScheduledStartTime { get; init; }

    /// <summary>Scheduled end (UTC).</summary>
    public DateTime ScheduledEndTime { get; init; }

    /// <summary>Unpaid break minutes.</summary>
    public int BreakMinutes { get; init; }

    /// <summary>Service delivery model.</summary>
    public ServiceType ServiceType { get; init; }

    /// <summary>Current shift status.</summary>
    public ShiftStatus Status { get; init; }

    /// <summary>Requirement labels derived from persisted non-PHI scheduling fields.</summary>
    public IReadOnlyList<string> RequirementLabels { get; init; } = [];

    /// <summary>True when assignment may proceed through the shift update endpoint.</summary>
    public bool IsAssignable { get; init; }

    /// <summary>Positive match reasons.</summary>
    public IReadOnlyList<string> MatchReasons { get; init; } = [];

    /// <summary>Blocking reasons.</summary>
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];
}
