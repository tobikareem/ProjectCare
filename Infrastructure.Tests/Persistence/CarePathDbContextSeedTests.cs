using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.Identity;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
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
        var context = scope.ServiceProvider.GetRequiredService<CarePathDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var configuration = CreateConfiguration(seedPassword: null);
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Production };

        // Act
        await CarePathDbContextSeed.SeedAsync(context, userManager, roleManager, configuration, environment);

        // Assert
        (await context.DomainUsers.CountAsync()).Should().Be(0);
        (await context.Set<ApplicationUser>().CountAsync()).Should().Be(0);
        (await roleManager.Roles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SeedAsync_WhenDevelopmentPasswordMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CarePathDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var configuration = CreateConfiguration(seedPassword: null);
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };

        // Act
        var act = async () => await CarePathDbContextSeed.SeedAsync(context, userManager, roleManager, configuration, environment);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Development seed password must be configured at 'SeedData:DefaultPassword' using user secrets or an environment variable.");
    }

    [Fact]
    public async Task SeedAsync_WhenEnvironmentIsDevelopment_SeedsSyntheticDataIdempotently()
    {
        // Arrange
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CarePathDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var configuration = CreateConfiguration("TestsOnly!2026");
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };

        // Act
        await CarePathDbContextSeed.SeedAsync(context, userManager, roleManager, configuration, environment);
        await CarePathDbContextSeed.SeedAsync(context, userManager, roleManager, configuration, environment);

        // Assert
        (await context.DomainUsers.IgnoreQueryFilters().CountAsync()).Should().Be(5);
        (await context.Set<ApplicationUser>().IgnoreQueryFilters().CountAsync()).Should().Be(5);
        (await roleManager.Roles.CountAsync()).Should().Be(Enum.GetNames<UserRole>().Length);
        (await context.Caregivers.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await context.Clients.IgnoreQueryFilters().CountAsync()).Should().Be(1);

        var adminIdentity = await userManager.FindByEmailAsync("admin@carepath.local");
        adminIdentity.Should().NotBeNull();
        (await userManager.IsInRoleAsync(adminIdentity!, UserRole.Admin.ToString())).Should().BeTrue();

        var coordinatorIdentity = await userManager.FindByEmailAsync("coordinator@carepath.local");
        coordinatorIdentity.Should().NotBeNull();
        coordinatorIdentity!.Id.Should().Be(Guid.Parse("66666666-6666-6666-6666-666666666666"));
        (await userManager.IsInRoleAsync(coordinatorIdentity, UserRole.Coordinator.ToString())).Should().BeTrue();

        var clinicianIdentity = await userManager.FindByEmailAsync("clinician@carepath.local");
        clinicianIdentity.Should().NotBeNull();
        clinicianIdentity!.Id.Should().Be(Guid.Parse("77777777-7777-7777-7777-777777777777"));
        (await userManager.IsInRoleAsync(clinicianIdentity, UserRole.Clinician.ToString())).Should().BeTrue();

        var client = await context.Clients.IgnoreQueryFilters().SingleAsync();
        client.MedicalConditions.Should().StartWith("Synthetic demo condition only");
        client.Allergies.Should().StartWith("Synthetic demo allergy only");
    }

    [Fact]
    public async Task SeedAsync_WhenSyntheticRowsWereSoftDeleted_ReactivatesWithoutCreatingDuplicates()
    {
        // Arrange
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CarePathDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var configuration = CreateConfiguration("TestsOnly!2026");
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };

        await CarePathDbContextSeed.SeedAsync(context, userManager, roleManager, configuration, environment);

        var admin = await context.DomainUsers.SingleAsync(user => user.Email == "admin@carepath.local");
        var caregiver = await context.Caregivers.SingleAsync();
        var client = await context.Clients.SingleAsync();
        admin.IsDeleted = true;
        caregiver.IsDeleted = true;
        client.IsDeleted = true;
        await context.SaveChangesAsync();

        // Act
        await CarePathDbContextSeed.SeedAsync(context, userManager, roleManager, configuration, environment);

        // Assert
        (await context.DomainUsers.IgnoreQueryFilters().CountAsync()).Should().Be(5);
        (await context.Set<ApplicationUser>().IgnoreQueryFilters().CountAsync()).Should().Be(5);
        (await context.Caregivers.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await context.Clients.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await context.DomainUsers.CountAsync()).Should().Be(5);
        (await context.Caregivers.CountAsync()).Should().Be(1);
        (await context.Clients.CountAsync()).Should().Be(1);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddDbContext<CarePathDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>()
            .AddEntityFrameworkStores<CarePathDbContext>()
            .AddDefaultTokenProviders();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
    }

    private static IConfiguration CreateConfiguration(string? seedPassword)
    {
        var values = new Dictionary<string, string?>();

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
