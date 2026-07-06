using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CarePath.Application.Abstractions.Auth;
using CarePath.Contracts.Auth;
using CarePath.Contracts.Common;
using CarePath.WebApi.Controllers;
using CarePath.WebApi.Middleware;
using CarePath.WebApi.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using InfrastructureJwtTokenService = CarePath.Infrastructure.Auth.JwtTokenService;

namespace CarePath.Application.Tests.WebApi;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task Login_WhenCredentialsAreValid_ReturnsTokenAndAuthenticatesProtectedEndpoint()
    {
        // Arrange
        var identity = new FakeIdentityService();
        using var server = CreateServer(identity);
        var client = server.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "clinician@example.test",
            Password = "ValidPassword1",
        });
        var token = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        var protectedResponse = await client.GetAsync("/api/test-protected");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        token.AccessToken.Should().NotBeNullOrWhiteSpace();
        token.RefreshToken.Should().Be("refresh-token-1");
        token.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
        token.Role.Should().Be(CarePath.Contracts.Enumerations.UserRole.Clinician);
        token.DisplayName.Should().Be("Synthetic Clinician");
        protectedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WhenFailureModesOccur_ReturnsIdenticalGenericUnauthorizedBodies()
    {
        // Arrange
        using var server = CreateServer(new FakeIdentityService());
        var client = server.CreateClient();

        var requests = new[]
        {
            new LoginRequest { Email = "unknown@example.test", Password = "ValidPassword1" },
            new LoginRequest { Email = "wrong-password@example.test", Password = "WrongPassword1" },
            new LoginRequest { Email = "locked@example.test", Password = "ValidPassword1" },
            new LoginRequest { Email = "inactive@example.test", Password = "ValidPassword1" },
        };

        // Act
        var responses = new List<HttpResponseMessage>();
        var bodies = new List<byte[]>();
        foreach (var request in requests)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", request);
            responses.Add(response);
            bodies.Add(await response.Content.ReadAsByteArrayAsync());
        }

        // Assert
        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Unauthorized);
        bodies.Skip(1).Should().OnlyContain(body => body.SequenceEqual(bodies[0]));

        var problem = JsonSerializer.Deserialize<ProblemDetailsResponse>(bodies[0], JsonOptions())
            ?? throw new InvalidOperationException("Problem details response could not be deserialized.");
        problem.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem.TraceId.Should().BeNull();
        problem.Errors.Should().ContainSingle(error =>
            error.Code == "auth.invalid_credentials" &&
            error.Message == "Invalid credentials.");
    }

    [Fact]
    public async Task Refresh_WhenTokenIsReused_ReturnsGenericUnauthorizedBody()
    {
        // Arrange
        using var server = CreateServer(new FakeIdentityService());
        var client = server.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "clinician@example.test",
            Password = "ValidPassword1",
        });
        var loginToken = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResponse>()
            ?? throw new InvalidOperationException("Login token response could not be deserialized.");

        // Act
        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = loginToken.RefreshToken,
        });
        var rotatedToken = await refreshResponse.Content.ReadFromJsonAsync<AuthTokenResponse>()
            ?? throw new InvalidOperationException("Refresh token response could not be deserialized.");
        var reuseResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = loginToken.RefreshToken,
        });
        var reuseBody = await reuseResponse.Content.ReadAsStringAsync();

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        rotatedToken.RefreshToken.Should().Be("refresh-token-2");
        reuseResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        reuseBody.Should().Contain("auth.invalid_credentials");
        reuseBody.Should().NotContain(loginToken.RefreshToken);
    }

    [Fact]
    public async Task Login_WhenRoleShapeIsInvalid_ReturnsGenericUnauthorizedBody()
    {
        // Arrange
        var identity = new FakeIdentityService(new HashSet<string>(StringComparer.Ordinal));
        using var server = CreateServer(identity);
        var client = server.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "clinician@example.test",
            Password = "ValidPassword1",
        });
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        body.Should().Contain("auth.invalid_credentials");
        body.Should().NotContain("clinician@example.test");
    }

    [Fact]
    public void AuthController_OnlyLoginAndRefreshAllowAnonymous()
    {
        var anonymousActions = typeof(AuthController)
            .GetMethods()
            .Where(method => method.DeclaringType == typeof(AuthController))
            .Where(method => method.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Any())
            .Select(method => method.Name)
            .ToArray();

        anonymousActions.Should().BeEquivalentTo(nameof(AuthController.Login), nameof(AuthController.Refresh));
    }

    private static TestServer CreateServer(FakeIdentityService identityService)
    {
        var configuration = CreateJwtConfiguration();
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IIdentityService>(identityService);
                services.AddSingleton<IJwtTokenService>(new InfrastructureJwtTokenService(configuration));
                services.AddCarePathAuthentication(configuration);
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(AuthController).Assembly)
                    .AddApplicationPart(typeof(AuthControllerTests).Assembly);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        return new TestServer(builder);
    }

    private static IConfiguration CreateJwtConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "CarePath.Tests",
                ["Jwt:Audience"] = "CarePath.Tests.Api",
                ["Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-32-bytes",
                ["Jwt:AccessTokenExpirationMinutes"] = "30",
            })
            .Build();
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class FakeIdentityService : IIdentityService
    {
        private readonly Guid userId = Guid.NewGuid();
        private readonly IReadOnlySet<string> roles;
        private string? currentRefreshToken;
        private int refreshIssueCount;

        public FakeIdentityService()
            : this(new HashSet<string>([ApplicationRoles.Clinician], StringComparer.Ordinal))
        {
        }

        public FakeIdentityService(IReadOnlySet<string> roles)
        {
            this.roles = roles;
        }

        public Task<IdentityUserResult> ValidateCredentialsAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            if (email == "clinician@example.test" && password == "ValidPassword1")
            {
                return Task.FromResult(Success());
            }

            return Task.FromResult(IdentityUserResult.Failed("InvalidCredentials"));
        }

        public Task<IdentityUserResult> GetUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(userId == this.userId ? Success() : IdentityUserResult.Failed("InvalidCredentials"));
        }

        public Task<string> IssueRefreshTokenAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            refreshIssueCount++;
            currentRefreshToken = $"refresh-token-{refreshIssueCount}";
            return Task.FromResult(currentRefreshToken);
        }

        public async Task<RefreshTokenRotationResult> RotateRefreshTokenAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(currentRefreshToken) || refreshToken != currentRefreshToken)
            {
                return RefreshTokenRotationResult.Failed("InvalidCredentials");
            }

            var rotatedToken = await IssueRefreshTokenAsync(userId, cancellationToken);
            return new RefreshTokenRotationResult(true, Success(), rotatedToken, null);
        }

        private IdentityUserResult Success()
        {
            return new IdentityUserResult(
                true,
                userId,
                "clinician@example.test",
                roles,
                null,
                "Synthetic Clinician");
        }
    }
}

[ApiController]
[Route("api/test-protected")]
public sealed class ProtectedTestController : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IActionResult Get()
    {
        return Ok();
    }
}
