namespace CarePath.Domain.Enumerations;

/// <summary>
/// Application roles that determine what a user can see and do within CarePath Health.
/// Roles are assigned at the User entity level and enforced by the API authorization layer.
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Full system administrator. Can manage all users, settings, and reports.
    /// Typically assigned to the agency owner or operations director.
    /// </summary>
    Admin = 1,

    /// <summary>
    /// Care coordinator. Assigns caregivers to clients, schedules shifts,
    /// reviews visit notes, and manages caregiver-client matching.
    /// </summary>
    Coordinator = 2,

    /// <summary>
    /// Caregiver (W-2 employee or 1099 contractor). Can view assigned shifts,
    /// check in/out via GPS, and submit visit notes and photos.
    /// </summary>
    Caregiver = 3,

    /// <summary>
    /// Client (care recipient or authorised family member). Can view their
    /// care plans, upcoming shifts, visit notes, and invoices.
    /// </summary>
    Client = 4,

    /// <summary>
    /// Facility manager for 1099 staffing placements. Can submit staffing
    /// requests, view contractor assignments, and approve facility invoices.
    /// </summary>
    FacilityManager = 5
}
