namespace CarePath.Contracts.Auth;

/// <summary>
/// Token refresh request (D-S6-2). CREDENTIAL SAFETY: never logged; failures return the same
/// generic <c>auth.invalid_credentials</c> code as login.
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>The refresh token issued at login.</summary>
    public string RefreshToken { get; init; } = string.Empty;
}
