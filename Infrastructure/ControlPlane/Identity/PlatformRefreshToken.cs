namespace CarePath.Infrastructure.ControlPlane.Identity;

/// <summary>
/// Server-tracked refresh token bound to a platform user and, for tenant sessions, to one
/// membership and organization (ADR 0003 §10.1). A credential artifact, not a domain entity.
/// </summary>
public class PlatformRefreshToken
{
    /// <summary>Row identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Owning platform user.</summary>
    public Guid PlatformUserId { get; set; }

    /// <summary>Membership the token was issued for; <c>null</c> for platform (operator) sessions.</summary>
    public Guid? MembershipId { get; set; }

    /// <summary>Organization the token was issued at, denormalized for host binding; <c>null</c> for platform sessions.</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>Base64 SHA-256 of the opaque token value (max 128). The plaintext is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>UTC expiry.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>UTC revocation time; <c>null</c> while the token is live.</summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>UTC issuance time. Not a <c>BaseEntity</c>, so the audit interceptor does not set this; the issuer must.</summary>
    public DateTime CreatedAtUtc { get; set; }
}
