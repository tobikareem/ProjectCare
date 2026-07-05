using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CarePath.Application.Abstractions.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CarePath.Infrastructure.Auth;

/// <summary>
/// Issues signed JWT access tokens for authenticated CarePath users.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private const string ExpirationMinutesKey = "Jwt:AccessTokenExpirationMinutes";
    private readonly IConfiguration configuration;

    /// <summary>Initializes a JWT token service.</summary>
    /// <param name="configuration">Runtime configuration. Signing material must come from user secrets or environment variables.</param>
    public JwtTokenService(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    /// <inheritdoc />
    public Task<JwtTokenResult> CreateTokenAsync(
        JwtTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationParameters = JwtTokenValidationParametersFactory.Create(configuration);
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(GetExpirationMinutes());
        var signingCredentials = new SigningCredentials(
            validationParameters.IssuerSigningKey,
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, request.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, request.UserId.ToString()),
            new(ClaimTypes.Email, request.Email),
        };

        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            claims.Add(new Claim("correlation_id", request.CorrelationId));
        }

        foreach (var role in request.Roles.OrderBy(role => role, StringComparer.Ordinal))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: validationParameters.ValidIssuer,
            audience: validationParameters.ValidAudience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: signingCredentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return Task.FromResult(new JwtTokenResult(accessToken, expiresAtUtc));
    }

    private int GetExpirationMinutes()
    {
        var configuredValue = configuration[ExpirationMinutesKey];
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return 60;
        }

        if (!int.TryParse(configuredValue, out var minutes) || minutes <= 0)
        {
            throw new InvalidOperationException("JWT access-token expiration must be a positive number of minutes.");
        }

        return minutes;
    }
}