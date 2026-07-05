using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Identity;

/// <summary>
/// Minimal user representation for lists and lookups. Carries no address or PHI fields.
/// </summary>
public class UserSummaryDto
{
    /// <summary>Domain user identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Display name ("First Last").</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Email address.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Contact phone number.</summary>
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>Application role.</summary>
    public UserRole Role { get; init; }

    /// <summary>False when the account is deactivated.</summary>
    public bool IsActive { get; init; }
}
