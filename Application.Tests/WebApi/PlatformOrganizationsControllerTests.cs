using System.Net;
using System.Net.Http.Headers;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Application.Organization.Services;
using CarePath.Application.Platform.Services;
using CarePath.Contracts.Common;
using CarePath.Contracts.Organization;
using CarePath.Contracts.Platform;
using CarePath.WebApi.Controllers;
using CarePath.WebApi.Middleware;
using CarePath.WebApi.Security;
using CarePath.WebApi.Tenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using InfrastructureJwtTokenService = CarePath.Infrastructure.Auth.JwtTokenService;

namespace CarePath.Application.Tests.WebApi;

/// <summary>
/// Pins the platform/tenant boundary at the HTTP layer (CP-04 FR-013): the same token is 403 or
/// 401 depending on where it is presented, and the controllers never run in the denied cases.
/// </summary>
public sealed class PlatformOrganizationsControllerTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();

    [Fact]
    public async Task PlatformRoute_WithPlatformTokenAtPlatformHost_Succeeds()
    {
        // Arrange
        using var host = CreateHost(OrganizationResolutionKind.Platform);
        var client = CreateClient(host, PlatformToken());

        // Act
        var response = await client.GetAsync("/api/platform/organizations?pageNumber=1&pageSize=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PlatformRoute_WithTenantAdminTokenAtItsOwnHost_IsForbidden()
    {
        // Arrange — requirements §2.2: tenant token cannot call platform endpoints (403 at the tenant host).
        using var host = CreateHost(OrganizationResolutionKind.Tenant);
        var client = CreateClient(host, TenantToken(ApplicationRoles.Admin));

        // Act
        var response = await client.GetAsync("/api/platform/organizations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PlatformRoute_WithTenantTokenAtThePlatformHost_IsUnauthorized()
    {
        // Arrange — kind mismatch fails token binding before any policy runs.
        using var host = CreateHost(OrganizationResolutionKind.Platform);
        var client = CreateClient(host, TenantToken(ApplicationRoles.Admin));

        // Act
        var response = await client.GetAsync("/api/platform/organizations");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        body.Should().Contain(UnauthorizedResponseWriter.Code);
    }

    [Fact]
    public async Task TenantRoute_WithPlatformTokenAtATenantHost_IsUnauthorized()
    {
        // Arrange — requirements §2.2: PlatformAdmin cannot call tenant endpoints.
        using var host = CreateHost(OrganizationResolutionKind.Tenant);
        var client = CreateClient(host, PlatformToken());

        // Act
        var response = await client.GetAsync("/api/organization/memberships");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MembershipRoute_WithNonAdminTenantToken_IsForbidden()
    {
        // Arrange
        using var host = CreateHost(OrganizationResolutionKind.Tenant);
        var client = CreateClient(host, TenantToken(ApplicationRoles.Coordinator));

        // Act
        var response = await client.GetAsync("/api/organization/memberships");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MembershipRoute_WithAdminTenantToken_Succeeds()
    {
        // Arrange
        using var host = CreateHost(OrganizationResolutionKind.Tenant);
        var client = CreateClient(host, TenantToken(ApplicationRoles.Admin));

        // Act
        var response = await client.GetAsync("/api/organization/memberships");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static HttpClient CreateClient(IHost host, string token)
    {
        var client = host.GetTestServer().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string PlatformToken() =>
        Jwt.CreateTokenAsync(new JwtTokenRequest(Guid.NewGuid(), "ops@carepath.local", new HashSet<string>([ApplicationRoles.PlatformAdmin], StringComparer.Ordinal), null, CarePathClaimTypes.PlatformTokenKind, null, null)).Result.AccessToken;

    private static string TenantToken(string role) =>
        Jwt.CreateTokenAsync(new JwtTokenRequest(Guid.NewGuid(), "admin@carepath.local", new HashSet<string>([role], StringComparer.Ordinal), null, CarePathClaimTypes.TenantTokenKind, OrganizationId, Guid.NewGuid())).Result.AccessToken;

    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "CarePath.Tests",
            ["Jwt:Audience"] = "CarePath.Tests.Api",
            ["Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-32-bytes",
            ["Jwt:AccessTokenExpirationMinutes"] = "30",
        })
        .Build();

    private static readonly InfrastructureJwtTokenService Jwt = new(Configuration);

    private static IHost CreateHost(OrganizationResolutionKind kind)
    {
        var platformService = new Mock<IPlatformOrganizationService>();
        platformService
            .Setup(service => service.ListAsync(It.IsAny<PagedRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<OrganizationSummaryDto>());
        var membershipService = new Mock<IOrganizationMembershipService>();
        membershipService
            .Setup(service => service.ListAsync(It.IsAny<PagedRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<MembershipDto>());

        return new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton(platformService.Object);
                    services.AddSingleton(membershipService.Object);
                    services.AddOrganizationResolution();
                    services.AddCarePathAuthentication(Configuration);
                    services
                        .AddControllers()
                        .AddApplicationPart(typeof(PlatformOrganizationsController).Assembly);
                });
                webBuilder.Configure(app =>
                {
                    app.Use(async (context, next) =>
                    {
                        var organization = context.RequestServices.GetRequiredService<OrganizationContext>();
                        if (kind == OrganizationResolutionKind.Platform)
                        {
                            organization.SetPlatform();
                        }
                        else
                        {
                            organization.SetTenant(new ResolvedOrganization(OrganizationId, "brightcare", "BrightCare"));
                        }

                        await next();
                    });
                    app.UseCarePathProblemDetails();
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .Start();
    }
}
