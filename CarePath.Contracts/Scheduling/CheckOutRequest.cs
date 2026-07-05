namespace CarePath.Contracts.Scheduling;

/// <summary>
/// Caregiver GPS check-out for a shift. Coordinates flow client → server only; no contract
/// ever returns raw coordinates back out.
/// </summary>
public class CheckOutRequest
{
    /// <summary>Shift being checked out of.</summary>
    public Guid ShiftId { get; init; }

    /// <summary>Device latitude at check-out.</summary>
    public double Latitude { get; init; }

    /// <summary>Device longitude at check-out.</summary>
    public double Longitude { get; init; }

    /// <summary>Device timestamp (UTC). Server time remains authoritative.</summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>Unpaid break minutes actually taken during the shift.</summary>
    public int? BreakMinutes { get; init; }
}
