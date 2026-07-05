using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CarePath.Infrastructure.Auth;

/// <summary>
/// Builds JWT token validation parameters from runtime configuration.
/// </summary>
public static class JwtTokenValidationParametersFactory
{
    private const string IssuerKey = "Jwt:Issuer";
    private const string AudienceKey = "Jwt:Audience";
    private const string SigningKeyKey = "Jwt:SigningKey";

    /// <summary>
    /// Creates token validation parameters using non-committed runtime configuration.
    /// </summary>
    /// <param name="configuration">Configuration containing JWT issuer, audience, and signing key.</param>
    /// <returns>Configured token validation parameters.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required JWT configuration is missing or too weak.</exception>
    public static TokenValidationParameters Create(IConfiguration configuration)
    {
        var issuer = GetRequired(configuration, IssuerKey);
        var audience = GetRequired(configuration, AudienceKey);
        var signingKey = GetRequired(configuration, SigningKeyKey);
        var signingKeyBytes = Encoding.UTF8.GetBytes(signingKey);

        if (signingKeyBytes.Length < 32)
        {
            throw new InvalidOperationException("JWT signing key must be at least 32 bytes.");
        }

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            RequireExpirationTime = true,
            RequireSignedTokens = true,
        };
    }

    private static string GetRequired(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Required JWT configuration '{key}' is missing.");
        }

        return value;
    }
}