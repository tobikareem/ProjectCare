namespace CarePath.Domain.Enumerations;

/// <summary>
/// Type of care service delivered, driving pricing tiers and margin targets.
/// </summary>
public enum ServiceType
{
    /// <summary>
    /// Care delivered at a client's home by a W-2 employee.
    /// Bill rate range: $30–45/hr. Target gross margin: 40–45 %.
    /// </summary>
    InHomeCare = 1,

    /// <summary>
    /// Care delivered inside a healthcare facility by a 1099 contractor.
    /// Bill rate range: $30–90/hr. Target gross margin: 25–30 %.
    /// </summary>
    FacilityStaffing = 2
}
