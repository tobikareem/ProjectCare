using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Auth;

/// <summary>
/// Successful authentication result (D-S6-2). Tokens are held in memory only client-side —
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

    /// <summary>The authenticated user's application role (drives client-side navigation only — the server re-checks every call).</summary>
    public UserRole Role { get; init; }

    /// <summary>Display name for the header/nav. Staff name only — never patient data.</summary>
    public string DisplayName { get; init; } = string.Empty;
}
