using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Identity;

/// <summary>
/// Base user entity representing all users in CarePath (Admin, Coordinator, Caregiver, Client, FacilityManager).
/// Contains personal contact information, address, role assignment, and authentication metadata.
/// </summary>
/// <remarks>
/// All users share this entity regardless of role. Role-specific data is stored in the
/// corresponding profile entities (Caregiver, Client). Email serves as the unique
/// authentication identifier across all user types.
/// State defaults to "Maryland" as the primary operating market for MVP.
/// </remarks>
public class User : BaseEntity
{
    /// <summary>User's first name. Required.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>User's last name. Required.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Email address — unique across all users. Used as the primary authentication identifier.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Primary phone number (US format; Maryland area codes 301, 410, 443, 667 most common).</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Street address line. Optional for some user types (e.g., Coordinators using office address).</summary>
    public string? Address { get; set; }

    /// <summary>City name.</summary>
    public string? City { get; set; }

    /// <summary>State abbreviation. Defaults to "Maryland" for MVP (primary operating market).</summary>
    public string? State { get; set; } = "Maryland";

    /// <summary>ZIP code (5 or 9 digits, e.g., "20850" or "20850-1234").</summary>
    public string? ZipCode { get; set; }

    /// <summary>User role determining access permissions and which profile entity is associated.</summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Indicates whether the user account is active. Inactive users cannot authenticate.
    /// Default: <c>true</c>. Set to <c>false</c> instead of deleting (soft-disable pattern).
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// UTC timestamp of the most recent successful login. <c>null</c> if the user has never logged in.
    /// Useful for detecting dormant accounts and HIPAA access audits.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    // Computed Properties

    /// <summary>Full display name composed of <see cref="FirstName"/> and <see cref="LastName"/>.</summary>
    public string FullName => $"{FirstName} {LastName}";
}
