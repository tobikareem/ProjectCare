namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.ServiceType</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum ServiceType
{
    /// <summary>In-home care delivered by W-2 caregivers.</summary>
    InHomeCare = 1,

    /// <summary>Facility staffing placements filled by 1099 contractors.</summary>
    FacilityStaffing = 2
}
