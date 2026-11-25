using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Organization;

/// <summary>A membership in the caller's organization. Staff-directory fields only; no PHI.</summary>
public sealed class MembershipDto
{
    /// <summary>Membership identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Platform user id (equals the tenant user id).</summary>
    public Guid PlatformUserId { get; init; }

    /// <summary>Tenant user id when a profile exists in this organization's data plane.</summary>
    public Guid? TenantUserId { get; init; }

    /// <summary>Login email.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Role in this organization.</summary>
    public UserRole Role { get; init; }

    /// <summary>Membership status.</summary>
    public MembershipStatus Status { get; init; }

    /// <summary>UTC timestamp of the most recent login at this organization.</summary>
    public DateTime? LastLoginAtUtc { get; init; }
}
