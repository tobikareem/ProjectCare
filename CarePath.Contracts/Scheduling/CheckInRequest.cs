namespace CarePath.Contracts.Scheduling;

/// <summary>
/// Caregiver GPS check-in for a shift. Coordinates flow client → server only; no contract
/// ever returns raw coordinates back out. The shift ID travels in the request body, not the URL.
/// </summary>
public class CheckInRequest
{
    /// <summary>Shift being checked into.</summary>
    public Guid ShiftId { get; init; }

    /// <summary>Device latitude at check-in.</summary>
    public double Latitude { get; init; }

    /// <summary>Device longitude at check-in.</summary>
    public double Longitude { get; init; }

    /// <summary>Device timestamp (UTC). Server time remains authoritative.</summary>
    public DateTime TimestampUtc { get; init; }
}
