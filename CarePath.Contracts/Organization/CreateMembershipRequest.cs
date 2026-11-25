using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Organization;

/// <summary>
/// Creates a membership in the caller's organization. When the email already belongs to a platform
/// identity it is linked; otherwise a new identity is created. The response never reveals which.
/// </summary>
public sealed class CreateMembershipRequest
{
    /// <summary>Login email.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Role in this organization.</summary>
    public UserRole Role { get; init; }

    /// <summary>First name (max 100).</summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>Last name (max 100).</summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>Contact phone (max 20).</summary>
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>Initial password (8–128 chars). Always required; ignored when the identity already exists. Never logged.</summary>
    public string TemporaryPassword { get; init; } = string.Empty;
}
