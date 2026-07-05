using CarePath.Application.Abstractions.Auth;

namespace CarePath.Application.Auth;

public sealed class DenyByDefaultObjectAuthorizationService : IObjectAuthorizationService
{
    public Task<ObjectAuthorizationResult> AuthorizeAsync(
        ObjectAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ObjectAuthorizationResult.Denied("NoObjectAuthorizationPolicy"));
    }
}
