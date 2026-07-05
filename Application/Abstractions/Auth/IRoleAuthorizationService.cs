namespace CarePath.Application.Abstractions.Auth;

public interface IRoleAuthorizationService
{
    bool IsInRole(ICurrentUserContext user, string role);

    bool IsInAnyRole(ICurrentUserContext user, IReadOnlySet<string> roles);
}