using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Admin;

/// <summary>
/// Admin request to change a user's role (D-S6-8). Server-enforced guardrails: cannot demote
/// the last active Admin; users with a caregiver/client profile cannot be role-changed away
/// from the profile's role. Applied atomically to the Domain user and the Identity role;
/// takes effect at the target's next sign-in.
/// </summary>
public class UpdateUserRoleRequest
{
    /// <summary>The new role.</summary>
    public UserRole Role { get; init; }
}
