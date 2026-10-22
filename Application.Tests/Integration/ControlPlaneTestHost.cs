using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CarePath.Application;
using CarePath.Application.Abstractions.Auth;
using CarePath.Contracts.Auth;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Platform;
using OrganizationEntity = CarePath.Domain.Entities.Platform.Organization;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using CarePath.Infrastructure.Auth;
using CarePath.Infrastructure.ControlPlane;
using CarePath.Infrastructure.ControlPlane.Identity;
using CarePath.Infrastructure.ControlPlane.Persistence;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using CarePath.Infrastructure.Persistence.Repositories;
using CarePath.WebApi.Controllers;
using CarePath.WebApi.Middleware;
using CarePath.WebApi.Security;
using CarePath.WebApi.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CarePath.Application.Tests.Integration;

/// <summary>
/// In-process CP-04 host (TASK-098): the real resolution middleware, JWT pipeline, control-plane
/// identity/membership services and controllers over in-memory control-plane and tenant contexts.
/// Two Active organizations share the tenant context (as the development seed does) so cross-host
/// isolation is proven by the control plane alone.
/// </summary>
public sealed class ControlPlaneTestHost : IAsyncDisposable
{
    /// <summary>Host that serves platform (operator) traffic.</summary>
    public const string PlatformHost = "platform.test";

    /// <summary>Host of the first organization.</summary>
    public const string BrightCareHost = "brightcare.test";

    /// <summary>Host of the second organization.</summary>
    public const string HelpingHandsHost = "helpinghands.test";

    /// <summary>A host no organization owns.</summary>
    public const string UnknownHost = "nobody.test";

    /// <summary>Every seeded account shares this password; the suite never logs it.</summary>
    public const string Password = "Integration-Pass-1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHost _host;

    private ControlPlaneTestHost(IHost host)
    {
        _host = host;
    }

    /// <summary>Organization id of BrightCare.</summary>
    public Guid BrightCareId { get; } = Guid.NewGuid();

    /// <summary>Organization id of Helping Hands.</summary>
    public Guid HelpingHandsId { get; } = Guid.NewGuid();

    /// <summary>Admin of BrightCare (no Helping Hands membership).</summary>
    public SeededUser BrightCareAdmin { get; } = new("alice@brightcare.test", "Alice Admin");

    /// <summary>Caregiver of Helping Hands only.</summary>
    public SeededUser HelpingHandsCaregiver { get; } = new("bob@helpinghands.test", "Bob Caregiver");

    /// <summary>Clinician holding Active memberships in both organizations.</summary>
    public SeededUser SharedClinician { get; } = new("carol@shared.test", "Carol Clinician");

    /// <summary>Platform operator with no organization membership.</summary>
    public SeededUser PlatformOperator { get; } = new("ops@platform.test", "Platform Operator");

    /// <summary>Counts requests that reached an authorized controller action.</summary>
    public ProbeCounter Probe { get; } = new();

    /// <summary>Builds and seeds the host.</summary>
    public static async Task<ControlPlaneTestHost> CreateAsync()
    {
        var controlPlaneDatabase = Guid.NewGuid().ToString();
        var tenantDatabase = Guid.NewGuid().ToString();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "CarePath.Integration",
                ["Jwt:Audience"] = "CarePath.Integration.Api",
                ["Jwt:SigningKey"] = "integration-signing-key-that-is-long-enough-32-bytes",
                ["Jwt:AccessTokenExpirationMinutes"] = "30",
                ["ControlPlane:PlatformHostNames:0"] = PlatformHost,
                ["ControlPlane:PlatformBaseDomain"] = "test",
                ["ControlPlane:BrandingCacheSeconds"] = "0",
                ["ControlPlane:AllowMultipleActiveOrganizations"] = "true",
            })
            .Build();

        ControlPlaneTestHost? instance = null;
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddLogging();
                    services.AddHttpContextAccessor();
                    services.AddScoped<AuditableEntityInterceptor>();
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddApplication();
                    services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
                    services.AddOrganizationResolution();
                    services.AddControlPlane(
                        options => options
                            .UseInMemoryDatabase(controlPlaneDatabase)
                            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)),
                        configuration.GetSection(ControlPlaneOptions.SectionName));
                    services.AddDbContext<CarePathDbContext>(options => options
                        .UseInMemoryDatabase(tenantDatabase)
                        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
                    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
                    services.AddScoped<IUnitOfWork, UnitOfWork>();
                    services.AddScoped<IJwtTokenService, JwtTokenService>();
                    services.AddCarePathAuthentication(configuration);
                    services.AddSingleton(provider => instance!.Probe);
                    services
                        .AddControllers()
                        .AddApplicationPart(typeof(AuthController).Assembly)
                        .AddApplicationPart(typeof(ControlPlaneTestHost).Assembly);
                });
                webBuilder.Configure(app =>
                {
                    // Same order as WebApi/Program.cs (minus HTTPS redirection, Swagger and CORS).
                    app.UseCarePathProblemDetails();
                    app.UseTrustedForwardedHeaders();
                    app.UseOrganizationResolution();
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/health/live", () => "Healthy").AllowAnonymous();
                        endpoints.MapControllers();
                    });
                });
            })
            .StartAsync();

        instance = new ControlPlaneTestHost(host);
        await instance.SeedAsync();
        return instance;
    }

    /// <summary>A client whose every request carries the given host header.</summary>
    /// <param name="host">The host to present.</param>
    /// <param name="accessToken">Optional bearer token.</param>
    public HttpClient ClientFor(string host, string? accessToken = null)
    {
        var client = _host.GetTestServer().CreateClient();
        client.BaseAddress = new Uri($"http://{host}/");
        client.DefaultRequestHeaders.Host = host;
        if (accessToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client;
    }

    /// <summary>Logs in at a host and returns the raw response (never throws on 401).</summary>
    public async Task<HttpResponseMessage> LoginAsync(string host, SeededUser user)
    {
        using var client = ClientFor(host);
        return await client.PostAsJsonAsync("api/auth/login", new LoginRequest { Email = user.Email, Password = Password });
    }

    /// <summary>Logs in at a host and returns the token response, failing the test when login is refused.</summary>
    public async Task<AuthTokenResponse> LoginExpectingSuccessAsync(string host, SeededUser user)
    {
        using var response = await LoginAsync(host, user);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Login at {host} returned {(int)response.StatusCode}.");
        }

        return (await response.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions))!;
    }

    /// <summary>Runs an action against the control-plane context.</summary>
    public async Task WithControlPlaneAsync(Func<ControlPlaneDbContext, Task> action)
    {
        using var scope = _host.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    private async Task SeedAsync()
    {
        using var scope = _host.Services.CreateScope();
        var controlPlane = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<CarePathDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();

        AddOrganization(controlPlane, BrightCareId, "brightcare", "BrightCare", BrightCareHost, BrandingColorToken.Teal700);
        AddOrganization(controlPlane, HelpingHandsId, "helpinghands", "Helping Hands", HelpingHandsHost, BrandingColorToken.Green);
        await controlPlane.SaveChangesAsync();

        await CreateUserAsync(userManager, tenant, BrightCareAdmin, isPlatformAdmin: false, UserRole.Admin);
        await CreateUserAsync(userManager, tenant, HelpingHandsCaregiver, isPlatformAdmin: false, UserRole.Caregiver);
        await CreateUserAsync(userManager, tenant, SharedClinician, isPlatformAdmin: false, UserRole.Clinician);
        await CreateUserAsync(userManager, tenant, PlatformOperator, isPlatformAdmin: true, null);

        controlPlane.OrganizationMemberships.AddRange(
            Membership(BrightCareId, BrightCareAdmin.Id, UserRole.Admin),
            Membership(HelpingHandsId, HelpingHandsCaregiver.Id, UserRole.Caregiver),
            Membership(BrightCareId, SharedClinician.Id, UserRole.Clinician),
            Membership(HelpingHandsId, SharedClinician.Id, UserRole.Clinician));
        await controlPlane.SaveChangesAsync();
        await tenant.SaveChangesAsync();
    }

    private static void AddOrganization(ControlPlaneDbContext controlPlane, Guid id, string slug, string displayName, string host, BrandingColorToken primary)
    {
        controlPlane.Organizations.Add(new OrganizationEntity
        {
            Id = id,
            LegalName = $"{displayName} LLC",
            DisplayName = displayName,
            Slug = slug,
            Status = OrganizationStatus.Active,
            ProvisioningState = ProvisioningState.Provisioned,
        });
        controlPlane.OrganizationDomains.Add(new OrganizationDomain
        {
            OrganizationId = id,
            HostName = host,
            IsPrimary = true,
            VerificationStatus = DomainVerificationStatus.Verified,
        });
        controlPlane.OrganizationBrandings.Add(new OrganizationBranding
        {
            OrganizationId = id,
            PrimaryColorToken = primary,
            AccentColorToken = BrandingColorToken.Orange,
        });
    }

    private static async Task CreateUserAsync(UserManager<PlatformUser> userManager, CarePathDbContext tenant, SeededUser seeded, bool isPlatformAdmin, UserRole? tenantRole)
    {
        var platformUser = new PlatformUser
        {
            Id = seeded.Id,
            UserName = seeded.Email,
            Email = seeded.Email,
            DisplayName = seeded.DisplayName,
            IsPlatformAdmin = isPlatformAdmin,
        };
        var created = await userManager.CreateAsync(platformUser, Password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException("Seed user could not be created.");
        }

        if (tenantRole is not null)
        {
            var names = seeded.DisplayName.Split(' ');
            tenant.DomainUsers.Add(new User
            {
                Id = seeded.Id,
                FirstName = names[0],
                LastName = names[1],
                Email = seeded.Email,
                PhoneNumber = "555-0100",
                Role = tenantRole.Value,
                IsActive = true,
                PlatformUserId = seeded.Id,
            });
        }
    }

    private static OrganizationMembership Membership(Guid organizationId, Guid userId, UserRole role) => new()
    {
        OrganizationId = organizationId,
        PlatformUserId = userId,
        TenantUserId = userId,
        Role = role,
        Status = MembershipStatus.Active,
    };

    /// <summary>A seeded account (synthetic; no real person).</summary>
    /// <param name="Email">Login email.</param>
    /// <param name="DisplayName">Two-word display name.</param>
    public sealed record SeededUser(string Email, string DisplayName)
    {
        /// <summary>Platform user id, which is also the tenant user id (D-CP04-02).</summary>
        public Guid Id { get; } = Guid.NewGuid();
    }

    /// <summary>Thread-safe counter the probe controller increments.</summary>
    public sealed class ProbeCounter
    {
        private int _count;

        /// <summary>Requests that reached the probe action.</summary>
        public int Count => Volatile.Read(ref _count);

        /// <summary>Records one invocation.</summary>
        public void Increment() => Interlocked.Increment(ref _count);
    }
}
