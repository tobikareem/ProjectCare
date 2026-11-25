using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.ControlPlane.Identity;
using CarePath.Infrastructure.ControlPlane.Persistence;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CarePath.Infrastructure.Tests.Persistence;

public class CarePathDbContextSeedTests
{
    [Fact]
    public async Task SeedAsync_WhenEnvironmentIsNotDevelopment_DoesNotSeedData()
    {
        // Arrange
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var (context, controlPlane, userManager) = Resolve(scope);
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Production };

        // Act
        await CarePathDbContextSeed.SeedAsync(context, controlPlane, userManager, CreateConfiguration(seedPassword: null), environment);

        // Assert
        (await context.DomainUsers.CountAsync()).Should().Be(0);
        (await controlPlane.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SeedAsync_WhenDevelopmentPasswordMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var (context, controlPlane, userManager) = Resolve(scope);
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };

        // Act
        var act = async () => await CarePathDbContextSeed.SeedAsync(context, controlPlane, userManager, CreateConfiguration(seedPassword: null), environment);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Development seed password must be configured at 'SeedData:DefaultPassword' using user secrets or an environment variable.");
    }

    [Fact]
    public async Task SeedAsync_WhenDefaultOrganizationIsMissing_Throws()
    {
        // Arrange
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var (context, controlPlane, userManager) = Resolve(scope);
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };

        // Act
        var act = async () => await CarePathDbContextSeed.SeedAsync(context, controlPlane, userManager, CreateConfiguration("TestsOnly!2026"), environment);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*default organization*");
    }

    [Fact]
    public async Task SeedAsync_WhenEnvironmentIsDevelopment_SeedsIdentitiesMembershipsAndSecondOrganizationIdempotently()
    {
        // Arrange
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var (context, controlPlane, userManager) = Resolve(scope);
        var defaultOrganization = await AddDefaultOrganizationAsync(controlPlane);
        var configuration = CreateConfiguration("TestsOnly!2026");
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };

        // Act
        await CarePathDbContextSeed.SeedAsync(context, controlPlane, userManager, configuration, environment);
        await CarePathDbContextSeed.SeedAsync(context, controlPlane, userManager, configuration, environment);

        // Assert
        (await context.DomainUsers.IgnoreQueryFilters().CountAsync()).Should().Be(5);
        (await context.DomainUsers.CountAsync(user => user.PlatformUserId == user.Id)).Should().Be(5);
        (await controlPlane.Users.CountAsync()).Should().Be(5);
        (await context.Caregivers.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await context.Clients.IgnoreQueryFilters().CountAsync()).Should().Be(1);

        var coordinator = await userManager.FindByEmailAsync("coordinator@carepath.local");
        coordinator.Should().NotBeNull();
        coordinator!.Id.Should().Be(Guid.Parse("66666666-6666-6666-6666-666666666666"));
        coordinator.IsPlatformAdmin.Should().BeFalse();
        (await userManager.CheckPasswordAsync(coordinator, "TestsOnly!2026")).Should().BeTrue();

        var defaultMemberships = await controlPlane.OrganizationMemberships.Where(membership => membership.OrganizationId == defaultOrganization.Id).ToListAsync();
        defaultMemberships.Should().HaveCount(5);
        defaultMemberships.Single(membership => membership.PlatformUserId == coordinator.Id).Role.Should().Be(UserRole.Coordinator);

        var second = await controlPlane.Organizations.SingleAsync(organization => organization.Slug == CarePathDbContextSeed.SecondOrganizationSlug);
        second.IsAdmissible.Should().BeTrue();
        (await controlPlane.OrganizationDomains.Where(domain => domain.OrganizationId == second.Id).Select(domain => domain.HostName).ToListAsync())
            .Should().Equal("helpinghands.localhost");
        (await controlPlane.OrganizationMemberships.CountAsync(membership => membership.OrganizationId == second.Id)).Should().Be(1);
        (await controlPlane.OrganizationDataPlanes.SingleAsync(dataPlane => dataPlane.OrganizationId == second.Id)).SecretReference
            .Should().Be("ConnectionStrings:DefaultConnection");

        var client = await context.Clients.IgnoreQueryFilters().SingleAsync();
        client.MedicalConditions.Should().StartWith("Synthetic demo condition only");
    }

    [Fact]
    public async Task SeedAsync_WhenSyntheticRowsWereSoftDeleted_ReactivatesWithoutCreatingDuplicates()
    {
        // Arrange
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var (context, controlPlane, userManager) = Resolve(scope);
        await AddDefaultOrganizationAsync(controlPlane);
        var configuration = CreateConfiguration("TestsOnly!2026");
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };

        await CarePathDbContextSeed.SeedAsync(context, controlPlane, userManager, configuration, environment);

        var admin = await context.DomainUsers.SingleAsync(user => user.Email == "admin@carepath.local");
        var caregiver = await context.Caregivers.SingleAsync();
        var client = await context.Clients.SingleAsync();
        admin.IsDeleted = true;
        caregiver.IsDeleted = true;
        client.IsDeleted = true;
        await context.SaveChangesAsync();

        // Act
        await CarePathDbContextSeed.SeedAsync(context, controlPlane, userManager, configuration, environment);

        // Assert
        (await context.DomainUsers.IgnoreQueryFilters().CountAsync()).Should().Be(5);
        (await controlPlane.Users.CountAsync()).Should().Be(5);
        (await context.Caregivers.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await context.Clients.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await context.DomainUsers.CountAsync()).Should().Be(5);
        (await context.Caregivers.CountAsync()).Should().Be(1);
        (await context.Clients.CountAsync()).Should().Be(1);
    }

    private static async Task<Organization> AddDefaultOrganizationAsync(ControlPlaneDbContext controlPlane)
    {
        var organization = new Organization
        {
            LegalName = "CarePath Health LLC",
            DisplayName = "CarePath Health",
            Slug = "carepath",
            Status = OrganizationStatus.Active,
            ProvisioningState = ProvisioningState.Provisioned,
        };
        controlPlane.Organizations.Add(organization);
        controlPlane.OrganizationDataPlanes.Add(new OrganizationDataPlane
        {
            OrganizationId = organization.Id,
            Provider = DataPlaneProvider.SqlServer,
            SecretReference = "ConnectionStrings:DefaultConnection",
            RegisteredAtUtc = DateTime.UtcNow,
        });
        await controlPlane.SaveChangesAsync();
        return organization;
    }

    private static (CarePathDbContext, ControlPlaneDbContext, UserManager<PlatformUser>) Resolve(IServiceScope scope) =>
        (scope.ServiceProvider.GetRequiredService<CarePathDbContext>(),
         scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>(),
         scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>());

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddDataProtection();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddDbContext<CarePathDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddDbContext<ControlPlaneDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services
            .AddIdentityCore<PlatformUser>()
            .AddEntityFrameworkStores<ControlPlaneDbContext>()
            .AddDefaultTokenProviders();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
    }

    private static IConfiguration CreateConfiguration(string? seedPassword)
    {
        var values = new Dictionary<string, string?>
        {
            ["ControlPlane:DefaultOrganization:Slug"] = "carepath",
        };

        if (seedPassword is not null)
        {
            values["SeedData:DefaultPassword"] = seedPassword;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "CarePath.Infrastructure.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
