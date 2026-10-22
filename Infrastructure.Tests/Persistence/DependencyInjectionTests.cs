using CarePath.Application.Abstractions.Auth;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Interfaces.Repositories;
using CarePath.Application.Abstractions.Audit;
using CarePath.Infrastructure;
using CarePath.Infrastructure.ControlPlane;
using CarePath.Infrastructure.ControlPlane.Audit;
using CarePath.Infrastructure.ControlPlane.Auth;
using CarePath.Infrastructure.ControlPlane.Identity;
using CarePath.Infrastructure.ControlPlane.Persistence;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace CarePath.Infrastructure.Tests.Persistence;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_WhenConnectionStringExists_RegistersPersistenceServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("Server=localhost;Database=CarePathHealth_Test;Integrated Security=true;Encrypt=True;TrustServerCertificate=True;");

        // Act
        services.AddInfrastructure(configuration);
        using var serviceProvider = BuildValidatedProvider(services);
        using var scope = serviceProvider.CreateScope();

        // Assert
        scope.ServiceProvider.GetRequiredService<CarePathDbContext>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IRepository<User>>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IClientAccessEvaluator>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IIdentityProvisioningService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AuditableEntityInterceptor>().Should().NotBeNull();
        serviceProvider.GetRequiredService<IHttpContextAccessor>().Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_WithConnectionStringOverload_RegistersClientAccessEvaluator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInfrastructure("Server=localhost;Database=CarePathHealth_Test;Integrated Security=true;Encrypt=True;TrustServerCertificate=True;");
        using var serviceProvider = BuildValidatedProvider(services);
        using var scope = serviceProvider.CreateScope();

        // Assert
        scope.ServiceProvider.GetRequiredService<IClientAccessEvaluator>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IIdentityProvisioningService>().Should().BeNull("identity provisioning lives in the control plane registration");
    }
    [Fact]
    public void AddInfrastructure_WhenConnectionStringMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(connectionString: null);

        // Act
        var act = () => services.AddInfrastructure(configuration);

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Connection string 'DefaultConnection' not found.");
    }

    [Fact]
    public void AddInfrastructure_WhenRegistered_ConfiguresSqlServerRetryAndNoSensitiveLogging()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("Server=localhost;Database=CarePathHealth_Test;Integrated Security=true;Encrypt=True;TrustServerCertificate=True;");

        // Act
        services.AddInfrastructure(configuration);
        using var serviceProvider = BuildValidatedProvider(services);
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CarePathDbContext>();

        // Assert
        dbContext.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.SqlServer");
        dbContext.Database.CreateExecutionStrategy().RetriesOnFailure.Should().BeTrue();
        dbContext.GetService<Microsoft.EntityFrameworkCore.Diagnostics.IDiagnosticsLogger<Microsoft.EntityFrameworkCore.DbLoggerCategory.Infrastructure>>()
            .Options
            .IsSensitiveDataLoggingEnabled
            .Should()
            .BeFalse();
    }

    [Fact]
    public void AddInfrastructure_WhenRegistered_ConfiguresIdentityDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("Server=localhost;Database=CarePathHealth_Test;Integrated Security=true;Encrypt=True;TrustServerCertificate=True;");

        // Act
        services.AddInfrastructure(configuration);
        using var serviceProvider = BuildValidatedProvider(services);
        using var scope = serviceProvider.CreateScope();
        var options = serviceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        // Assert
        options.Password.RequiredLength.Should().Be(8);
        options.Password.RequireDigit.Should().BeTrue();
        options.Password.RequireLowercase.Should().BeTrue();
        options.Password.RequireUppercase.Should().BeTrue();
        options.Password.RequireNonAlphanumeric.Should().BeFalse();
        options.User.RequireUniqueEmail.Should().BeTrue();
        scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>().Should().NotBeNull();
        scope.ServiceProvider.GetService<RoleManager<IdentityRole<Guid>>>().Should().BeNull("the control plane has no Identity roles (D-CP04-04)");
        scope.ServiceProvider.GetRequiredService<IIdentityService>().Should().BeOfType<PlatformIdentityService>();
        scope.ServiceProvider.GetRequiredService<IIdentityProvisioningService>().Should().BeOfType<PlatformIdentityProvisioningService>();
    }

    [Fact]
    public void AddInfrastructure_WhenRegistered_ResolvesControlPlaneServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("Server=localhost;Database=CarePathHealth_Test;Integrated Security=true;Encrypt=True;TrustServerCertificate=True;");

        // Act
        services.AddInfrastructure(configuration);
        using var serviceProvider = BuildValidatedProvider(services);
        using var scope = serviceProvider.CreateScope();

        // Assert
        scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IControlPlaneUnitOfWork>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IPlatformAuditLogger>().Should().BeOfType<EfPlatformAuditLogger>();
        scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>().Should().NotBeNull();
        serviceProvider.GetRequiredService<HostDenialAuditThrottle>().Should().NotBeNull();
        serviceProvider.GetRequiredService<IOptions<ControlPlaneOptions>>().Value.BrandingCacheSeconds.Should().Be(60);
    }

    [Fact]
    public void AddInfrastructure_WhenRegistered_RegistersPlatformUserTokenProvidersOnly()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("Server=localhost;Database=CarePathHealth_Test;Integrated Security=true;Encrypt=True;TrustServerCertificate=True;");

        // Act
        services.AddInfrastructure(configuration);
        using var serviceProvider = BuildValidatedProvider(services);
        var identityOptions = serviceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        // Assert — with tenant Identity gone, the global provider map belongs to PlatformUser.
        identityOptions.Tokens.ProviderMap.Should().ContainKey(TokenOptions.DefaultProvider);
        identityOptions.Tokens.ProviderMap[TokenOptions.DefaultProvider].ProviderType
            .Should().Be(typeof(DataProtectorTokenProvider<PlatformUser>));
    }

    [Fact]
    public void AddInfrastructure_WhenControlPlaneConnectionEqualsTenantConnection_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        const string shared = "Server=localhost;Database=CarePathHealth_Test;Integrated Security=true;Encrypt=True;TrustServerCertificate=True;";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = shared,
                ["ConnectionStrings:ControlPlaneConnection"] = " " + shared.ToUpperInvariant()
            })
            .Build();

        // Act
        var act = () => services.AddInfrastructure(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*must not equal the tenant connection string*");
    }

    [Theory]
    [InlineData("Database=CarePathHealth_Test;Server=localhost;User Id=cp;Password=x;Encrypt=True;")]
    [InlineData("Data Source=LOCALHOST;Initial Catalog=carepathhealth_test;Integrated Security=true;Encrypt=True;TrustServerCertificate=True;")]
    [InlineData("Server=localhost;Database=CarePathHealth_Test;Application Name=ControlPlane;Encrypt=True;")]
    [InlineData("Server=tcp:localhost,1433;Database=CarePathHealth_Test;Encrypt=True;")]
    [InlineData("Server=127.0.0.1;Database=CarePathHealth_Test;Encrypt=True;")]
    [InlineData("Server=(local);Database=CarePathHealth_Test;Encrypt=True;")]
    [InlineData("Host=localhost;Database=CarePathHealth_Test;Username=cp")]
    [InlineData("Server=localhost;Integrated Security=true;Encrypt=True;")]
    public void AddInfrastructure_WhenControlPlaneTargetsTheTenantDatabaseUnderAnotherSpelling_Throws(string controlPlane)
    {
        // Arrange — review finding M6: key order, synonyms, credentials and extra parameters must not
        // disguise the same server and catalog.
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=CarePathHealth_Test;Integrated Security=true;Encrypt=True;TrustServerCertificate=True;",
                ["ConnectionStrings:ControlPlaneConnection"] = controlPlane
            })
            .Build();

        // Act
        var act = () => services.AddInfrastructure(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*must not equal the tenant connection string*");
    }

    [Fact]
    public void TargetsSameDatabase_WhenOnlyTheCatalogDiffers_IsFalse()
    {
        ControlPlaneServiceCollectionExtensions.TargetsSameDatabase(
                "Server=localhost;Database=CarePathHealth;Encrypt=True;",
                "Server=localhost;Database=CarePathControlPlane;Encrypt=True;")
            .Should().BeFalse();
        ControlPlaneServiceCollectionExtensions.TargetsSameDatabase(
                "Host=devpi.local;Port=5432;Database=carepath;Username=u",
                "Host=devpi.local;Port=5432;Database=carepath_controlplane;Username=u")
            .Should().BeFalse();
        ControlPlaneServiceCollectionExtensions.TargetsSameDatabase(
                "Server=localhost\\SQLEXPRESS;Database=CarePath;Encrypt=True;",
                "Server=localhost;Database=CarePath;Encrypt=True;")
            .Should().BeFalse("a named instance is a different server process");
    }

    [Fact]
    public void AddInfrastructure_WhenControlPlaneConnectionStringIsBlank_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=CarePathHealth_Test;Integrated Security=true;Encrypt=True;TrustServerCertificate=True;",
                ["ConnectionStrings:ControlPlaneConnection"] = "   "
            })
            .Build();

        // Act
        var act = () => services.AddInfrastructure(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*ControlPlaneSqlServerConnection*");
    }

    [Fact]
    public void EntityTypeConfigurations_WhenInspected_LiveInATenantOrControlPlaneNamespaceOrAreAppliedExplicitly()
    {
        // Arrange — CarePathDbContext scans only the tenant namespace; a configuration placed
        // anywhere else would be dropped from the model without any signal.
        const string tenantNamespace = "CarePath.Infrastructure.Persistence.Configurations";
        const string controlPlaneNamespace = "CarePath.Infrastructure.ControlPlane.Persistence.Configurations";
        var configurationTypes = typeof(CarePathDbContext).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.GetInterfaces().Any(contract =>
                contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)))
            .ToList();

        // Act
        var misplaced = configurationTypes
            .Where(type => type.Namespace != tenantNamespace
                && type.Namespace?.StartsWith(tenantNamespace + ".", StringComparison.Ordinal) != true
                && type.Namespace != controlPlaneNamespace)
            .Select(type => type.FullName)
            .ToList();

        // Assert
        configurationTypes.Should().NotBeEmpty();
        misplaced.Should().BeEmpty();
    }

    [Fact]
    public void AddInfrastructure_WhenControlPlaneConnectionStringMissing_ThrowsAndNeverFallsBackToTenantConnection()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=CarePathHealth_Test;Integrated Security=true;Encrypt=True;TrustServerCertificate=True;"
            })
            .Build();

        // Act
        var act = () => services.AddInfrastructure(configuration);

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*ControlPlaneSqlServerConnection*never falls back to the tenant connection string*");
    }

    [Fact]
    public void AddInfrastructure_WhenProviderSpecificControlPlaneConnectionExists_PrefersItOverGenericName()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=CarePathHealth_Test;Integrated Security=true;Encrypt=True;TrustServerCertificate=True;",
                ["ConnectionStrings:ControlPlaneConnection"] = "Server=generic;Database=Generic;Integrated Security=true;Encrypt=True;",
                ["ConnectionStrings:ControlPlaneSqlServerConnection"] = "Server=specific;Database=Specific;Integrated Security=true;Encrypt=True;"
            })
            .Build();

        // Act
        services.AddInfrastructure(configuration);
        using var serviceProvider = BuildValidatedProvider(services);
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();

        // Assert
        context.Database.GetConnectionString().Should().Contain("specific").And.NotContain("generic");
    }

    private static ServiceProvider BuildValidatedProvider(IServiceCollection services)
    {
        // Per-request contexts are registered by WebApi, not Infrastructure; stub them so
        // ValidateOnBuild can construct the control-plane services that depend on them.
        services.AddScoped(_ => Mock.Of<CarePath.Application.Abstractions.Tenancy.IOrganizationContext>());
        services.AddScoped(_ => Mock.Of<ICurrentUserContext>());
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
    }

    private static IConfiguration CreateConfiguration(string? connectionString)
    {
        var values = new Dictionary<string, string?>();

        if (connectionString is not null)
        {
            values["ConnectionStrings:DefaultConnection"] = connectionString;
            values["ConnectionStrings:ControlPlaneConnection"] = connectionString.Replace("CarePathHealth_Test", "CarePathControlPlane_Test", StringComparison.Ordinal);
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
