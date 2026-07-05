using System.Net.Http.Headers;

namespace CarePath.Client.Http;

/// <summary>
/// Delegating handler that attaches the bearer token from <see cref="IAccessTokenProvider"/>
/// to every outgoing request. Register with <c>IHttpClientFactory</c> via
/// <c>AddHttpMessageHandler&lt;AuthorizationMessageHandler&gt;()</c>.
/// </summary>
public sealed class AuthorizationMessageHandler : DelegatingHandler
{
    private readonly IAccessTokenProvider _tokenProvider;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="tokenProvider">Source of the current access token.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="tokenProvider"/> is null.</exception>
    public AuthorizationMessageHandler(IAccessTokenProvider tokenProvider) =>
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
