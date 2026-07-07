using CarePath.Application.Abstractions.Auth;
using CarePath.WebApi.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CarePath.Application.Tests.WebApi;

public sealed class AuthConfigurationTests
{
    [Fact]
    public void AddCarePathAuthentication_WhenConfigured_RegistersFallbackAndRolePolicies()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateJwtConfiguration();

        // Act
        services.AddCarePathAuthentication(configuration);
        using var provider = services.BuildServiceProvider();
        var authenticationOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        var authorizationOptions = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        var jwtOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        authenticationOptions.DefaultAuthenticateScheme.Should().Be(JwtBearerDefaults.AuthenticationScheme);
        authenticationOptions.DefaultChallengeScheme.Should().Be(JwtBearerDefaults.AuthenticationScheme);
        authenticationOptions.DefaultForbidScheme.Should().Be(JwtBearerDefaults.AuthenticationScheme);
        jwtOptions.IncludeErrorDetails.Should().BeFalse();
        authorizationOptions.FallbackPolicy.Should().NotBeNull();
        authorizationOptions.FallbackPolicy!.AuthenticationSchemes
            .Should()
            .ContainSingle(JwtBearerDefaults.AuthenticationScheme);
        authorizationOptions.FallbackPolicy!.Requirements
            .Any(requirement => requirement is DenyAnonymousAuthorizationRequirement)
            .Should()
            .BeTrue();

        foreach (var role in ApplicationRoles.All)
        {
            var policy = authorizationOptions.GetPolicy(role);
            policy.Should().NotBeNull($"role policy {role} must be registered");
            policy!.AuthenticationSchemes.Should().ContainSingle(JwtBearerDefaults.AuthenticationScheme);
            policy!.Requirements
                .Any(requirement =>
                    requirement is RolesAuthorizationRequirement rolesRequirement &&
                    rolesRequirement.AllowedRoles.Contains(role, StringComparer.Ordinal))
                .Should()
                .BeTrue($"role policy {role} must require the matching role");
        }
    }

    private static IConfiguration CreateJwtConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "CarePath.Tests",
                ["Jwt:Audience"] = "CarePath.Tests.Api",
                ["Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-32-bytes",
            })
            .Build();
    }
}
