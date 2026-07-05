namespace CarePath.Application.Abstractions.Auth;

public interface IObjectAuthorizationService
{
    Task<ObjectAuthorizationResult> AuthorizeAsync(
        ObjectAccessRequest request,
        CancellationToken cancellationToken = default);
}