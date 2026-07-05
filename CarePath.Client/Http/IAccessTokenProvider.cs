namespace CarePath.Client.Http;

/// <summary>
/// Platform-agnostic source of the current JWT access token. Blazor WebAssembly and MAUI
/// supply their own implementations (browser storage vs secure device storage); this library
/// never persists tokens itself.
/// </summary>
public interface IAccessTokenProvider
{
    /// <summary>
    /// Returns the current access token, or <c>null</c> when the user is not authenticated.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>The bearer token string, or <c>null</c>.</returns>
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
