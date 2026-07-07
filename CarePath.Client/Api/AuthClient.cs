using CarePath.Contracts.Auth;
using CarePath.Contracts.Common;

namespace CarePath.Client.Api;

/// <summary>
/// Typed client for <c>/api/auth</c> (D-S6-2). Failures surface the single generic
/// <c>auth.invalid_credentials</c> error; this client never logs requests or tokens.
/// </summary>
public sealed class AuthClient : ApiClientBase
{
    /// <summary>Creates the client.</summary>
    /// <param name="httpClient">HTTP client configured with the API base address.</param>
    public AuthClient(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>Signs in with email and password.</summary>
    /// <param name="request">The login request. Never logged.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The token response on success; the generic auth error on failure.</returns>
    public Task<ApiResponse<AuthTokenResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<LoginRequest, AuthTokenResponse>("api/auth/login", request, cancellationToken);

    /// <summary>Exchanges a refresh token for a new token pair (rotation server-side).</summary>
    /// <param name="request">The refresh request. Never logged.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The new token response on success; the generic auth error on failure.</returns>
    public Task<ApiResponse<AuthTokenResponse>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<RefreshTokenRequest, AuthTokenResponse>("api/auth/refresh", request, cancellationToken);
}
