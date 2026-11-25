namespace CarePath.Application.Auth.Services;

/// <summary>Organization-bound login and refresh (CP-04 FR-008..011).</summary>
public interface ILoginService
{
    /// <summary>Validates credentials and issues a session bound to the resolved organization or the platform.</summary>
    Task<LoginResult> LoginAsync(string email, string password, string? correlationId, CancellationToken cancellationToken = default);

    /// <summary>Rotates a refresh token and re-validates organization and membership state.</summary>
    Task<LoginResult> RefreshAsync(string refreshToken, string? correlationId, CancellationToken cancellationToken = default);
}
