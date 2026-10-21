using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Platform;

/// <summary>
/// The sole source of truth for a platform user's role in one organization
/// (ADR 0003 §10). <c>(OrganizationId, PlatformUserId)</c> is unique.
/// </summary>
/// <remarks>
/// <see cref="SecurityVersion"/> increments atomically with every role or status change
/// (ADR 0003 §10.1). CP-04 records it; CP-05 validates it per request (D-CP04-05).
/// </remarks>
public class OrganizationMembership : BaseEntity
{
    /// <summary>Organization the membership belongs to.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Navigation to the organization.</summary>
    public Organization? Organization { get; set; }

    /// <summary>
    /// Control-plane identity user id. Equal to the tenant <c>User.Id</c> by construction
    /// (D-CP04-02); no cross-database foreign key exists.
    /// </summary>
    public Guid PlatformUserId { get; set; }

    /// <summary>
    /// Tenant-side <c>User.Id</c> when a tenant profile exists in the organization's data plane.
    /// <c>null</c> for organizations whose data plane is not provisioned.
    /// </summary>
    public Guid? TenantUserId { get; set; }

    /// <summary>One of the six tenant roles. A platform administrator is never a membership role.</summary>
    public UserRole Role { get; set; }

    /// <summary>Whether the user may log in at this organization's hosts.</summary>
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;

    /// <summary>Monotonic version bumped on every role or status change.</summary>
    public int SecurityVersion { get; set; }

    /// <summary>UTC timestamp of the most recent successful login at this organization.</summary>
    public DateTime? LastLoginAtUtc { get; set; }

    /// <summary>True when the membership admits login: active and not soft-deleted.</summary>
    public bool IsActive => Status == MembershipStatus.Active && !IsDeleted;
}
