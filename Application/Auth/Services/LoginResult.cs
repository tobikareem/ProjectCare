using CarePath.Contracts.Auth;

namespace CarePath.Application.Auth.Services;

/// <summary>Outcome of a login or refresh. Every failure is the same shape so the API emits one generic body.</summary>
/// <param name="Succeeded">True when a session was issued.</param>
/// <param name="Token">The issued session when successful.</param>
public sealed record LoginResult(bool Succeeded, AuthTokenResponse? Token)
{
    /// <summary>The single failure value.</summary>
    public static LoginResult Failed { get; } = new(false, null);
}
