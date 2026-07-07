using CarePath.Client.Http;

namespace CarePath.Web.Security;

internal sealed class EmptyAccessTokenProvider : IAccessTokenProvider
{
    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }
}
