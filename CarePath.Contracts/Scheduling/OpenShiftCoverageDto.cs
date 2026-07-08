using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Scheduling;

/// <summary>
/// Admin/Coordinator-safe row for the Schedule coverage queue. Uses display labels already
/// authorized by the schedule view and excludes notes, GPS, and rate fields.
/// </summary>
public class OpenShiftCoverageDto
{
    /// <summary>Open shift identifier.</summary>
    public Guid ShiftId { get; init; }

    /// <summary>Client identifier used by existing shift assignment requests.</summary>
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

    /// <summary>Best currently assignable caregiver labels.</summary>
    public IReadOnlyList<string> BestMatches { get; init; } = [];
}
