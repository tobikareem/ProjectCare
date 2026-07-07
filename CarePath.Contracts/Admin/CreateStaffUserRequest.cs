using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Admin;

/// <summary>
/// Admin request to provision a STAFF account (D-S6-8): Coordinator, Clinician,
/// FacilityManager, or Admin only — caregivers and clients are created through their own
/// profile workflows. Validators reject Caregiver/Client roles here.
/// </summary>
/// <remarks>
/// CREDENTIAL SAFETY (D-S4-5): <see cref="TemporaryPassword"/> is never committed, logged,
/// echoed, or returned; request-body logging is excluded on provisioning routes.
/// </remarks>
public class CreateStaffUserRequest
{
    /// <summary>First name.</summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>Last name.</summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>Unique email address (becomes the login).</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Contact phone number.</summary>
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>One-time temporary password. Never logged or echoed.</summary>
    public string? TemporaryPassword { get; init; }

    /// <summary>Staff role to assign: Coordinator, Clinician, FacilityManager, or Admin.</summary>
    public UserRole Role { get; init; }
}
