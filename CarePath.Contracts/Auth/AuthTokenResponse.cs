using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Auth;

/// <summary>
/// Successful authentication result (D-S6-2, CP-04). Tokens are held in memory only client-side —
/// never persisted to browser storage. Contains no PHI.
/// </summary>
public class AuthTokenResponse
{
    /// <summary>JWT bearer access token.</summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>Access token expiry (UTC).</summary>
    public DateTime ExpiresAtUtc { get; init; }

    /// <summary>Refresh token for obtaining a new access token.</summary>
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>Whether this is an organization-bound or a platform session.</summary>
    public AuthTokenKind TokenKind { get; init; } = AuthTokenKind.Tenant;

    /// <summary>
    /// The user's role in the organization (drives client-side navigation only — the server
    /// re-checks every call). <c>null</c> for platform sessions.
    /// </summary>
    public UserRole? Role { get; init; }

    /// <summary>Display name for the header/nav. Staff name only — never patient data.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Organization the session is bound to; <c>null</c> for platform sessions.</summary>
    public Guid? OrganizationId { get; init; }

    /// <summary>Organization slug; <c>null</c> for platform sessions.</summary>
    public string? OrganizationSlug { get; init; }

    /// <summary>Organization display name; <c>null</c> for platform sessions.</summary>
    public string? OrganizationDisplayName { get; init; }

    /// <summary>Membership the session is bound to; <c>null</c> for platform sessions.</summary>
    public Guid? MembershipId { get; init; }
}
