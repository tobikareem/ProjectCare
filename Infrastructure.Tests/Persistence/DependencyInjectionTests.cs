using CarePath.Application.Abstractions.Auth;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Interfaces.Repositories;
using CarePath.Infrastructure;
using CarePath.Infrastructure.Identity;
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
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>().Should().NotBeNull();
    }

    private static ServiceProvider BuildValidatedProvider(IServiceCollection services)
    {
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
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
