using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Application.Auth.Services;
using CarePath.Contracts.Auth;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using CarePath.WebApi.Controllers;
using CarePath.WebApi.Middleware;
using CarePath.WebApi.Security;
using CarePath.WebApi.Tenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Linq.Expressions;
using InfrastructureJwtTokenService = CarePath.Infrastructure.Auth.JwtTokenService;

namespace CarePath.Application.Tests.WebApi;

public sealed class AuthControllerTests
{
    private static readonly Guid OrganizationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid OtherOrganizationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    [Fact]
    public async Task Login_WhenCredentialsAreValid_ReturnsOrganizationBoundTokenAndAuthenticatesProtectedEndpoint()
    {
        // Arrange
        using var server = CreateServer(new FakeIdentityService());
        var client = server.GetTestClient();

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
        token.TokenKind.Should().Be(CarePath.Contracts.Enumerations.AuthTokenKind.Tenant);
        token.Role.Should().Be(CarePath.Contracts.Enumerations.UserRole.Clinician);
        token.OrganizationId.Should().Be(OrganizationId);
        token.OrganizationSlug.Should().Be("brightcare");
        token.MembershipId.Should().NotBeNull();
        token.DisplayName.Should().Be("Synthetic Clinician");
        protectedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedEndpoint_WhenTokenWasIssuedForAnotherOrganization_IsRejectedBeforeTheController()
    {
        // Arrange — token minted for BrightCare, presented at a host that resolved to another organization.
        using var issuingServer = CreateServer(new FakeIdentityService());
        var login = await issuingServer.GetTestClient().PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "clinician@example.test", Password = "ValidPassword1" });
        var token = await login.Content.ReadFromJsonAsync<AuthTokenResponse>();

        using var otherServer = CreateServer(new FakeIdentityService(), resolvedOrganizationId: OtherOrganizationId);
        var client = otherServer.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        // Act
        var response = await client.GetAsync("/api/test-protected");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        body.Should().Contain(UnauthorizedResponseWriter.Code);
    }

    [Fact]
    public async Task Login_WhenFailureModesOccur_ReturnsIdenticalGenericUnauthorizedBodies()
    {
        // Arrange
        using var server = CreateServer(new FakeIdentityService());
        var client = server.GetTestClient();

        var requests = new[]
        {
            new LoginRequest { Email = "unknown@example.test", Password = "ValidPassword1" },
            new LoginRequest { Email = "wrong-password@example.test", Password = "WrongPassword1" },
            new LoginRequest { Email = "locked@example.test", Password = "ValidPassword1" },
            new LoginRequest { Email = "no-membership@example.test", Password = "ValidPassword1" },
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
        var client = server.GetTestClient();
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
        rotatedToken.OrganizationId.Should().Be(OrganizationId);
        reuseResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        reuseBody.Should().Contain("auth.invalid_credentials");
        reuseBody.Should().NotContain(loginToken.RefreshToken);
    }

    [Fact]
    public async Task Login_WhenNoMembershipExists_ReturnsGenericBodyAndAuditsWithoutEmail()
    {
        // Arrange
        var audit = new RecordingAuditLogger();
        using var server = CreateServer(new FakeIdentityService(), auditLogger: audit);
        var client = server.GetTestClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "no-membership@example.test",
            Password = "ValidPassword1",
        });
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        body.Should().NotContain("no-membership@example.test");
        audit.Entries.Should().ContainSingle(entry => entry.Action == PlatformAuditAction.LoginDeniedNoMembership && entry.OrganizationId == OrganizationId);
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

    private static IHost CreateServer(
        FakeIdentityService identityService,
        Guid? resolvedOrganizationId = null,
        IPlatformAuditLogger? auditLogger = null)
    {
        var configuration = CreateJwtConfiguration();
        var organizationId = resolvedOrganizationId ?? OrganizationId;
        return new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<IIdentityService>(identityService);
                    services.AddSingleton<IJwtTokenService>(new InfrastructureJwtTokenService(configuration));
                    services.AddSingleton(auditLogger ?? new RecordingAuditLogger());
                    services.AddSingleton(CreateControlPlane(identityService.UserId));
                    services.AddSingleton(CreateTenant());
                    services.AddScoped<ILoginService, LoginService>();
                    services.AddOrganizationResolution();
                    services.AddCarePathAuthentication(configuration);
                    services
                        .AddControllers()
                        .AddApplicationPart(typeof(AuthController).Assembly)
                        .AddApplicationPart(typeof(AuthControllerTests).Assembly);
                });
                webBuilder.Configure(app =>
                {
                    // Stand-in for the resolution middleware: every request is at a resolved tenant host.
                    app.Use(async (context, next) =>
                    {
                        context.RequestServices.GetRequiredService<OrganizationContext>()
                            .SetTenant(new ResolvedOrganization(organizationId, "brightcare", "BrightCare"));
                        await next();
                    });
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .Start();
    }

    private static IControlPlaneUnitOfWork CreateControlPlane(Guid memberUserId)
    {
        var membership = new OrganizationMembership
        {
            OrganizationId = OrganizationId,
            PlatformUserId = memberUserId,
            TenantUserId = memberUserId,
            Role = UserRole.Clinician,
            Status = MembershipStatus.Active,
        };

        var memberships = new Mock<IRepository<OrganizationMembership>>();
        memberships
            .Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<OrganizationMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<OrganizationMembership, bool>> predicate, CancellationToken _) =>
                new[] { membership }.Where(predicate.Compile()).ToList());
        memberships
            .Setup(repository => repository.GetByIdAsync(membership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        memberships
            .Setup(repository => repository.UpdateAsync(It.IsAny<OrganizationMembership>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IControlPlaneUnitOfWork>();
        unitOfWork.SetupGet(work => work.Memberships).Returns(memberships.Object);
        unitOfWork.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return unitOfWork.Object;
    }

    private static IUnitOfWork CreateTenant()
    {
        var users = new Mock<IRepository<User>>();
        users.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new User { Id = id, FirstName = "Synthetic", LastName = "Clinician", Email = "clinician@example.test", PhoneNumber = "555-0100", Role = UserRole.Clinician });
        users.Setup(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(work => work.Users).Returns(users.Object);
        unitOfWork.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return unitOfWork.Object;
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

    private sealed class RecordingAuditLogger : IPlatformAuditLogger
    {
        public List<PlatformAuditEntry> Entries { get; } = [];

        public Task LogAsync(PlatformAuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        private readonly Guid noMembershipUserId = Guid.NewGuid();
        private string? currentRefreshToken;
        private Guid? currentMembershipId;
        private Guid? currentOrganizationId;
        private int refreshIssueCount;

        public Guid UserId { get; } = Guid.NewGuid();

        public Task<IdentityUserResult> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            if (password != "ValidPassword1")
            {
                return Task.FromResult(IdentityUserResult.Failed("InvalidCredentials"));
            }

            return Task.FromResult(email switch
            {
                "clinician@example.test" => Success(),
                "no-membership@example.test" => new IdentityUserResult(true, noMembershipUserId, email, "No Membership", false, null),
                "locked@example.test" => IdentityUserResult.Failed("LockedOut"),
                _ => IdentityUserResult.Failed("InvalidCredentials"),
            });
        }

        public Task<IdentityUserResult> GetUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId == UserId ? Success() : IdentityUserResult.Failed("UserNotFound"));

        public Task<string> IssueRefreshTokenAsync(Guid userId, Guid? membershipId, Guid? organizationId, CancellationToken cancellationToken = default)
        {
            refreshIssueCount++;
            currentRefreshToken = $"refresh-token-{refreshIssueCount}";
            currentMembershipId = membershipId;
            currentOrganizationId = organizationId;
            return Task.FromResult(currentRefreshToken);
        }

        public async Task<RefreshTokenRotationResult> RotateRefreshTokenAsync(string refreshToken, RefreshTokenBinding expectedBinding, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(currentRefreshToken) || refreshToken != currentRefreshToken || expectedBinding.OrganizationId != currentOrganizationId)
            {
                return RefreshTokenRotationResult.Failed("InvalidCredentials");
            }

            var rotatedToken = await IssueRefreshTokenAsync(UserId, currentMembershipId, currentOrganizationId, cancellationToken);
            return new RefreshTokenRotationResult(true, Success(), rotatedToken, currentMembershipId, currentOrganizationId, null);
        }

        private IdentityUserResult Success() =>
            new(true, UserId, "clinician@example.test", "Synthetic Clinician", false, null);
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
