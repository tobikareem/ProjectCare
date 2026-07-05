namespace CarePath.Application.Abstractions.Auth;

public interface IIdentityService
{
    Task<IdentityUserResult> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<IdentityUserResult> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}