using CarePath.Contracts.Auth;

namespace CarePath.Client.Http;

/// <summary>
/// In-memory token store per D-S6-2: tokens live only in this instance's memory — never in
/// localStorage/sessionStorage or any persisted store. Register as a singleton (WASM) or
/// per-session service; the session ends when the app instance does.
/// </summary>
public sealed class InMemoryAccessTokenProvider : IAccessTokenProvider
{
    private readonly object _gate = new();
    private AuthTokenResponse? _session;

    /// <summary>Stores the current session tokens after login or refresh.</summary>
    /// <param name="session">The token response from <c>AuthClient</c>.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="session"/> is null.</exception>
    public void SetSession(AuthTokenResponse session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lock (_gate)
        {
            _session = session;
        }
    }

    /// <summary>Clears the session (logout).</summary>
    public void ClearSession()
    {
        lock (_gate)
        {
            _session = null;
        }
    }

    /// <summary>The current session's refresh token, for the refresh flow. Null when signed out.</summary>
    public string? CurrentRefreshToken
    {
        get
        {
            lock (_gate)
            {
                return _session?.RefreshToken;
            }
        }
    }

    /// <summary>The current session metadata (role, display name, expiry). Null when signed out.</summary>
    public AuthTokenResponse? CurrentSession
    {
        get
        {
            lock (_gate)
            {
                return _session;
            }
        }
    }

    /// <inheritdoc />
    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var token = _session is not null && _session.ExpiresAtUtc > DateTime.UtcNow
                ? _session.AccessToken
                : null;
            return Task.FromResult(token);
        }
    }
}
