namespace CarePath.Contracts.Auth;

/// <summary>
/// Login request (D-S6-2). CREDENTIAL SAFETY: this request body must never be logged and
/// <see cref="Password"/> must never be echoed in validation errors. Login failures return a
/// single generic <c>auth.invalid_credentials</c> code regardless of cause.
/// </summary>
public class LoginRequest
{
    /// <summary>Account email.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Account password. Never logged, never echoed.</summary>
    public string Password { get; init; } = string.Empty;
}
