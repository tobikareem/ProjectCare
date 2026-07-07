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

    Task<string> IssueRefreshTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<RefreshTokenRotationResult> RotateRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}
