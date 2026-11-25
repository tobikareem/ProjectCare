using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Organization;

/// <summary>Changes a membership's role.</summary>
public sealed class UpdateMembershipRoleRequest
{
    /// <summary>New role.</summary>
    public UserRole Role { get; init; }
}
