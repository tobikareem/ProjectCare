using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Application.Organization.Services;
using CarePath.Contracts.Organization;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.ControlPlane;
using CarePath.WebApi.Controllers;
using CarePath.WebApi.Middleware;
using CarePath.WebApi.Security;
using CarePath.WebApi.Tenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using InfrastructureJwtTokenService = CarePath.Infrastructure.Auth.JwtTokenService;

namespace CarePath.Application.Tests.WebApi;

public sealed class OrganizationResolutionMiddlewareTests
{
    private static readonly Guid BrightCareId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Fact]
    public async Task Invoke_WhenHostResolvesToTenant_PopulatesContextAndContinues()
    {
        // Arrange
        using var host = CreateHost(new FakeResolver());
        var client = CreateClient(host, "BrightCare.Test:443");

        // Act
        var response = await client.GetAsync("/api/resolution-probe");
        var probe = await response.Content.ReadFromJsonAsync<ProbeResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        probe!.Kind.Should().Be(nameof(OrganizationResolutionKind.Tenant));
        probe.OrganizationId.Should().Be(BrightCareId);
        probe.Slug.Should().Be("brightcare");
    }

    [Fact]
    public async Task Invoke_WhenHostIsAConfiguredPlatformHost_MarksPlatformWithoutQueryingTheResolver()
    {
        // Arrange
        var resolver = new FakeResolver();
        using var host = CreateHost(resolver);
        var client = CreateClient(host, "Platform.Test");

        // Act
        var response = await client.GetAsync("/api/resolution-probe");
        var probe = await response.Content.ReadFromJsonAsync<ProbeResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        probe!.Kind.Should().Be(nameof(OrganizationResolutionKind.Platform));
        probe.OrganizationId.Should().BeNull();
        resolver.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Invoke_WhenHostDoesNotResolve_ReturnsBodyByteIdenticalToInvalidCredentialsAndNeverReachesTheEndpoint()
    {
        // Arrange
        var resolver = new FakeResolver();
        using var host = CreateHost(resolver);
        var probeCounter = host.Services.GetRequiredService<ProbeCounter>();
        var unknownClient = CreateClient(host, "unknown.test");
        var pendingClient = CreateClient(host, "pending.test");
        var suspendedClient = CreateClient(host, "suspended.test");
        var unverifiedClient = CreateClient(host, "unverified.test");
        var malformedClient = CreateClient(host, "bad_host.test");
        var tenantClient = CreateClient(host, "brightcare.test");

        // Act
        var denied = new List<byte[]>();
        foreach (var client in new[] { unknownClient, pendingClient, suspendedClient, unverifiedClient, malformedClient })
        {
            var response = await client.GetAsync("/api/resolution-probe");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            denied.Add(await response.Content.ReadAsByteArrayAsync());
        }

        var invalidCredentials = await tenantClient.GetAsync("/api/resolution-unauthorized");
        var invalidCredentialsBody = await invalidCredentials.Content.ReadAsByteArrayAsync();

        // Assert
        invalidCredentials.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        denied.Should().OnlyContain(body => body.SequenceEqual(invalidCredentialsBody));
        probeCounter.Count.Should().Be(0);
        var problem = JsonSerializer.Deserialize<ProblemDetailsResponse>(invalidCredentialsBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        problem.TraceId.Should().BeNull();
        problem.Errors.Should().ContainSingle(error => error.Code == UnauthorizedResponseWriter.Code);
    }

    [Fact]
    public async Task Invoke_WhenHostIsMalformed_AuditsASentinelHashSoTheThrottleApplies()
    {
        // Arrange — D-CP04-06: a flood of unparseable Host headers must not bypass the audit budget.
        var audit = new RecordingAuditLogger();
        using var host = CreateHost(new FakeResolver(), audit);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Host = "[::1]";

        // Act
        var response = await client.GetAsync("/api/clients");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var entry = audit.Entries.Should().ContainSingle().Which;
        entry.Action.Should().Be(PlatformAuditAction.HostResolutionDenied);
        entry.HostHash.Should().HaveLength(64).And.NotContain("::1");
    }

    [Fact]
    public async Task Invoke_WhenHostDoesNotResolve_AuditsAHostHashAndNeverTheHost()
    {
        // Arrange
        var audit = new RecordingAuditLogger();
        using var host = CreateHost(new FakeResolver(), audit);
        var client = CreateClient(host, "unknown.test");

        // Act
        await client.GetAsync("/api/resolution-probe");

        // Assert
        var entry = audit.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PlatformAuditAction.HostResolutionDenied);
        entry.HostHash.Should().HaveLength(64).And.NotContain("unknown");
        entry.OrganizationId.Should().BeNull();
    }

    [Fact]
    public async Task Invoke_WhenHostDoesNotResolveOnTheBrandingRoute_ReturnsStandardNotFound()
    {
        // Arrange
        using var host = CreateHost(new FakeResolver());
        var client = CreateClient(host, "unknown.test");

        // Act
        var response = await client.GetAsync("/api/organization/branding");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().Contain("resource.not_found").And.NotContain("unknown.test").And.NotContain("traceId");
    }

    [Fact]
    public async Task Invoke_WhenHostResolvesOnTheBrandingRoute_ReturnsBrandingForThatTenant()
    {
        // Arrange
        using var host = CreateHost(new FakeResolver());
        var client = CreateClient(host, "brightcare.test");

        // Act
        var response = await client.GetAsync("/api/organization/branding");
        var branding = await response.Content.ReadFromJsonAsync<OrganizationBrandingDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        branding!.Slug.Should().Be("brightcare");
        branding.DisplayName.Should().Be("BrightCare");
    }

    [Fact]
    public async Task Invoke_WhenHealthPathIsRequested_SkipsResolution()
    {
        // Arrange
        var resolver = new FakeResolver();
        using var host = CreateHost(resolver);
        var client = CreateClient(host, "unknown.test");

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        resolver.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Invoke_WhenControlPlaneIsUnavailable_Returns503WithoutDetail()
    {
        // Arrange
        using var host = CreateHost(new FakeResolver(throwOnResolve: true));
        var client = CreateClient(host, "brightcare.test");

        // Act
        var response = await client.GetAsync("/api/resolution-probe");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        body.Should().NotContain("Exception").And.NotContain("brightcare");
    }

    [Fact]
    public async Task Invoke_WhenXForwardedHostComesFromAnUnlistedSource_UsesTheRawHost()
    {
        // Arrange — the remote address is loopback; only 10.0.0.0/8 is trusted.
        var resolver = new FakeResolver();
        using var host = CreateHost(resolver, knownNetworks: ["10.0.0.0/8"], remoteAddress: "192.0.2.10");
        var client = CreateClient(host, "unknown.test");
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "brightcare.test");

        // Act
        var response = await client.GetAsync("/api/resolution-probe");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        resolver.Calls.Should().Equal("unknown.test");
    }

    [Fact]
    public async Task Invoke_WhenNoProxiesAreTrusted_IgnoresXForwardedHostFromAnySource()
    {
        // Arrange — empty trust list must mean "trust nobody", never "trust everyone".
        var resolver = new FakeResolver();
        using var host = CreateHost(resolver, knownNetworks: [], remoteAddress: "192.0.2.10");
        var client = CreateClient(host, "unknown.test");
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "brightcare.test");

        // Act
        var response = await client.GetAsync("/api/resolution-probe");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        resolver.Calls.Should().Equal("unknown.test");
    }

    [Fact]
    public async Task Invoke_WhenDeniedOrUnavailable_KeepsHeadersSetEarlierInThePipeline()
    {
        // Arrange — a header set before resolution (as CORS does) must survive the 401 and 503 writers.
        using var deniedHost = CreateHost(new FakeResolver(), markerHeader: true);
        using var outageHost = CreateHost(new FakeResolver(throwOnResolve: true), markerHeader: true);

        // Act
        var denied = await CreateClient(deniedHost, "unknown.test").GetAsync("/api/resolution-probe");
        var outage = await CreateClient(outageHost, "brightcare.test").GetAsync("/api/resolution-probe");

        // Assert
        denied.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        denied.Headers.Should().ContainKey("X-Pipeline-Marker");
        outage.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        outage.Headers.Should().ContainKey("X-Pipeline-Marker");
    }

    [Fact]
    public async Task Invoke_WhenXForwardedHostComesFromAKnownProxy_IsHonored()
    {
        // Arrange — the stub connection address is 10.1.2.3, inside the trusted network.
        var resolver = new FakeResolver();
        using var host = CreateHost(resolver, knownNetworks: ["10.0.0.0/8"], remoteAddress: "10.1.2.3");
        var client = CreateClient(host, "internal-ingress");
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "brightcare.test");

        // Act
        var response = await client.GetAsync("/api/resolution-probe");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        resolver.Calls.Should().Equal("brightcare.test");
    }

    private static HttpClient CreateClient(IHost host, string hostHeader)
    {
        var client = host.GetTestServer().CreateClient();
        client.DefaultRequestHeaders.Host = hostHeader;
        return client;
    }

    private static IHost CreateHost(
        FakeResolver resolver,
        IPlatformAuditLogger? auditLogger = null,
        string[]? knownNetworks = null,
        string? remoteAddress = null,
        bool markerHeader = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "CarePath.Tests",
                ["Jwt:Audience"] = "CarePath.Tests.Api",
                ["Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-32-bytes",
                ["Jwt:AccessTokenExpirationMinutes"] = "30",
            })
            .Build();

        return new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.Configure<ControlPlaneOptions>(options =>
                    {
                        options.PlatformHostNames = ["platform.test"];
                        options.TrustedProxies.KnownNetworks = knownNetworks ?? [];
                    });
                    services.AddSingleton<IOrganizationHostResolver>(resolver);
                    services.AddSingleton(auditLogger ?? new RecordingAuditLogger());
                    services.AddSingleton<ProbeCounter>();
                    services.AddSingleton<IBrandingCache, PassThroughBrandingCache>();
                    var brandings = new Mock<CarePath.Domain.Interfaces.Repositories.IRepository<CarePath.Domain.Entities.Platform.OrganizationBranding>>();
                    brandings
                        .Setup(repository => repository.FindAsync(
                            It.IsAny<System.Linq.Expressions.Expression<Func<CarePath.Domain.Entities.Platform.OrganizationBranding, bool>>>(),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync([]);
                    var controlPlane = new Mock<CarePath.Domain.Interfaces.Repositories.IControlPlaneUnitOfWork>();
                    controlPlane.SetupGet(unitOfWork => unitOfWork.Brandings).Returns(brandings.Object);
                    services.AddSingleton(controlPlane.Object);
                    services.AddScoped<IOrganizationBrandingQuery, OrganizationBrandingQuery>();
                    services.AddOrganizationResolution();
                    services.AddSingleton<IJwtTokenService>(new InfrastructureJwtTokenService(configuration));
                    services.AddCarePathAuthentication(configuration);
                    services.AddHealthChecks();
                    services
                        .AddControllers()
                        .AddApplicationPart(typeof(OrganizationBrandingController).Assembly)
                        .AddApplicationPart(typeof(OrganizationResolutionMiddlewareTests).Assembly);
                });
                webBuilder.Configure(app =>
                {
                    if (remoteAddress is not null)
                    {
                        app.Use(async (context, next) =>
                        {
                            context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
                            await next();
                        });
                    }

                    if (markerHeader)
                    {
                        app.Use(async (context, next) =>
                        {
                            context.Response.Headers["X-Pipeline-Marker"] = "set";
                            await next();
                        });
                    }

                    app.UseCarePathProblemDetails();
                    app.UseTrustedForwardedHeaders();
                    app.UseRouting();
                    app.UseOrganizationResolution();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapHealthChecks("/health/live").AllowAnonymous();
                        endpoints.MapControllers();
                    });
                });
            })
            .Start();
    }

    private sealed record ProbeResponse(string Kind, Guid? OrganizationId, string? Slug);

    private sealed class FakeResolver : IOrganizationHostResolver
    {
        private readonly bool _throwOnResolve;

        public FakeResolver(bool throwOnResolve = false)
        {
            _throwOnResolve = throwOnResolve;
        }

        public List<string> Calls { get; } = [];

        public Task<HostResolution> ResolveAsync(string normalizedHost, CancellationToken cancellationToken = default)
        {
            Calls.Add(normalizedHost);
            if (_throwOnResolve)
            {
                throw new TimeoutException("control plane timed out");
            }

            return Task.FromResult(normalizedHost switch
            {
                "brightcare.test" => HostResolution.Resolved(new ResolvedOrganization(BrightCareId, "brightcare", "BrightCare")),
                "pending.test" or "suspended.test" => HostResolution.Denied(HostDenialReason.OrganizationNotAdmissible),
                "unverified.test" => HostResolution.Denied(HostDenialReason.UnverifiedDomain),
                _ => HostResolution.Denied(HostDenialReason.UnknownHost)
            });
        }
    }

    private sealed class RecordingAuditLogger : IPlatformAuditLogger
    {
        public List<PlatformAuditEntry> Entries { get; } = [];

        public Task LogAsync(PlatformAuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class PassThroughBrandingCache : IBrandingCache
    {
        public Task<OrganizationBrandingDto> GetOrCreateAsync(Guid organizationId, Func<CancellationToken, Task<OrganizationBrandingDto>> factory, CancellationToken cancellationToken = default) =>
            factory(cancellationToken);

        public void Invalidate(Guid organizationId)
        {
        }
    }
}

/// <summary>Counts endpoint invocations so tests can prove a denied request never reached a controller.</summary>
public sealed class ProbeCounter
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Increment() => Interlocked.Increment(ref _count);
}

[ApiController]
[Route("api/resolution-probe")]
public sealed class ResolutionProbeController : ControllerBase
{
    private readonly IOrganizationContext _context;
    private readonly ProbeCounter _counter;

    public ResolutionProbeController(IOrganizationContext context, ProbeCounter counter)
    {
        _context = context;
        _counter = counter;
    }

    [HttpGet]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public IActionResult Get()
    {
        _counter.Increment();
        return Ok(new { Kind = _context.Kind.ToString(), _context.OrganizationId, _context.Slug });
    }
}

[ApiController]
[Route("api/resolution-unauthorized")]
public sealed class ResolutionUnauthorizedController : ControllerBase
{
    [HttpGet]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public IActionResult Get() => UnauthorizedResponseWriter.Result();
}
