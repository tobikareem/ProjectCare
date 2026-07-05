namespace CarePath.Application.Abstractions.Auth;

public interface IJwtTokenService
{
    Task<JwtTokenResult> CreateTokenAsync(
        JwtTokenRequest request,
        CancellationToken cancellationToken = default);
}