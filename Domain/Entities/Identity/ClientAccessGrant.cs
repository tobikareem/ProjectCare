using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Identity;

/// <summary>
/// Explicit object-level access grant that allows a Client-role user, such as a family proxy,
/// to access records for a care recipient client profile.
/// </summary>
/// <remarks>
/// Grants are PHI-adjacent access-control records. Reads and writes must be audited by the
/// Application layer without logging PHI values or internal authorization reason codes.
/// </remarks>
public class ClientAccessGrant : BaseEntity
{
    /// <summary>User receiving access to the client profile.</summary>
    public Guid GranteeUserId { get; set; }

    /// <summary>Navigation to the user receiving access.</summary>
    public User GranteeUser { get; set; } = null!;

    /// <summary>Client profile the grant authorizes access to.</summary>
    public Guid ClientId { get; set; }

    /// <summary>Navigation to the client profile the grant authorizes access to.</summary>
    public Client Client { get; set; } = null!;

    /// <summary>Scope of access this grant provides.</summary>
    public AccessScope AccessScope { get; set; } = AccessScope.PatientFacing;

    /// <summary>User who issued the grant.</summary>
    public Guid GrantedByUserId { get; set; }

    /// <summary>Navigation to the user who issued the grant.</summary>
    public User GrantedByUser { get; set; } = null!;

    /// <summary>UTC timestamp when access was granted.</summary>
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>User who revoked the grant, if it has been revoked.</summary>
    public Guid? RevokedByUserId { get; set; }

    /// <summary>Navigation to the user who revoked the grant, if it has been revoked.</summary>
    public User? RevokedByUser { get; set; }

    /// <summary>UTC timestamp when the grant was revoked, if it has been revoked.</summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Indicates whether the grant has been revoked.</summary>
    public bool IsRevoked => RevokedAtUtc.HasValue;
}
