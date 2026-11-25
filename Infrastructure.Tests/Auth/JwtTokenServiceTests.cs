using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CarePath.Application.Abstractions.Auth;
using CarePath.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CarePath.Infrastructure.Tests.Auth;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public async Task CreateTokenAsync_WhenConfigurationIsValid_IssuesValidRoleToken()
    {
        // Arrange
        var configuration = CreateJwtConfiguration();
        var service = new JwtTokenService(configuration);
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var request = new JwtTokenRequest(
            userId,
            "auth-user@carepath.local",
            new HashSet<string>(new[] { ApplicationRoles.Admin, ApplicationRoles.Clinician }, StringComparer.Ordinal),
            "trace-token-test",
            CarePathClaimTypes.TenantTokenKind,
            organizationId,
            membershipId);

        // Act
        var result = await service.CreateTokenAsync(request);

        // Assert
        result.TokenType.Should().Be("Bearer");
        result.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);

        var principal = ValidateToken(configuration, result.AccessToken);
        principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(userId.ToString());
        principal.FindFirstValue(ClaimTypes.Email).Should().Be("auth-user@carepath.local");
        principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value)
            .Should()
            .BeEquivalentTo(ApplicationRoles.Admin, ApplicationRoles.Clinician);
        principal.FindFirstValue("correlation_id").Should().Be("trace-token-test");
        principal.FindFirstValue(CarePathClaimTypes.TokenKind).Should().Be(CarePathClaimTypes.TenantTokenKind);
        principal.FindFirstValue(CarePathClaimTypes.OrganizationId).Should().Be(organizationId.ToString());
        principal.FindFirstValue(CarePathClaimTypes.MembershipId).Should().Be(membershipId.ToString());
    }

    [Fact]
    public async Task CreateTokenAsync_WhenPlatformToken_OmitsOrganizationAndMembershipClaims()
    {
        // Arrange
        var configuration = CreateJwtConfiguration();
        var service = new JwtTokenService(configuration);
        var request = new JwtTokenRequest(
            Guid.NewGuid(),
            "operator@carepath.local",
            new HashSet<string>([ApplicationRoles.PlatformAdmin], StringComparer.Ordinal),
            null,
            CarePathClaimTypes.PlatformTokenKind,
            null,
            null);

        // Act
        var result = await service.CreateTokenAsync(request);
        var principal = ValidateToken(configuration, result.AccessToken);

        // Assert
        principal.FindFirstValue(CarePathClaimTypes.TokenKind).Should().Be(CarePathClaimTypes.PlatformTokenKind);
        principal.FindFirst(CarePathClaimTypes.OrganizationId).Should().BeNull();
        principal.FindFirst(CarePathClaimTypes.MembershipId).Should().BeNull();
        principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Should().Equal(ApplicationRoles.PlatformAdmin);
    }

    [Fact]
    public async Task CreateTokenAsync_WhenSigningKeyMissing_ThrowsGenericConfigurationError()
    {
        // Arrange
        var configuration = CreateJwtConfiguration(includeSigningKey: false);
        var service = new JwtTokenService(configuration);
        var request = new JwtTokenRequest(
            Guid.NewGuid(),
            "auth-user@carepath.local",
            new HashSet<string>(StringComparer.Ordinal),
            null,
            CarePathClaimTypes.TenantTokenKind,
            Guid.NewGuid(),
            Guid.NewGuid());

        // Act
        var act = () => service.CreateTokenAsync(request);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Required JWT configuration 'Jwt:SigningKey' is missing.");
    }

    private static ClaimsPrincipal ValidateToken(IConfiguration configuration, string accessToken)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(
            accessToken,
            JwtTokenValidationParametersFactory.Create(configuration),
            out var validatedToken)
            .Also(_ => validatedToken.Should().BeOfType<JwtSecurityToken>());
    }

    private static IConfiguration CreateJwtConfiguration(bool includeSigningKey = true)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "CarePath.Tests",
            ["Jwt:Audience"] = "CarePath.Tests.Api",
            ["Jwt:AccessTokenExpirationMinutes"] = "30",
        };

        if (includeSigningKey)
        {
            values["Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-32-bytes";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}

internal static class AssertionExtensions
{
    public static T Also<T>(this T value, Action<T> assertion)
    {
        assertion(value);
        return value;
    }
}