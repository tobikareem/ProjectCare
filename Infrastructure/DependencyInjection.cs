using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Storage;
using CarePath.Domain.Interfaces.Repositories;
using CarePath.Infrastructure.Audit;
using CarePath.Infrastructure.Auth;
using CarePath.Infrastructure.Identity;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using CarePath.Infrastructure.Persistence.Repositories;
using CarePath.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarePath.Infrastructure;

/// <summary>
/// Registers Infrastructure layer services for persistence, identity, repositories, and interceptors.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds the Infrastructure layer using the configured SQL Server connection string.
    /// </summary>
    /// <param name="services">Service collection to register with.</param>
    /// <param name="configuration">Application configuration containing <c>ConnectionStrings:DefaultConnection</c>.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the <c>DefaultConnection</c> connection string is missing.
    /// </exception>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddInfrastructure(connectionString);
        services.AddSingleton(configuration);
        if (configuration.GetValue<bool>("Storage:EnableLocalPrivateStorage"))
        {
            services.AddScoped<IFileStorageService, LocalFileStorageService>();
        }

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IIdentityService, IdentityService>();

        return services;
    }

    /// <summary>
    /// Adds the Infrastructure layer using the supplied SQL Server connection string.
    /// </summary>
    /// <param name="services">Service collection to register with.</param>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<CarePathDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(CarePathDbContext).Assembly.GetName().Name);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            });
        });

        services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<CarePathDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IClientAccessEvaluator, ClientAccessEvaluator>();
        services.AddScoped<IIdentityProvisioningService, IdentityProvisioningService>();
        services.AddScoped<IPhiAuditLogger, LoggingPhiAuditLogger>();
        services.AddScoped<IFileStorageService, DisabledFileStorageService>();

        return services;
    }
}
