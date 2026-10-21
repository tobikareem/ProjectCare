using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Infrastructure.ControlPlane.Auth;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Infrastructure.ControlPlane.Bootstrap;
using CarePath.Infrastructure.ControlPlane.Resolution;
using CarePath.Domain.Interfaces.Repositories;
using CarePath.Infrastructure.ControlPlane.Audit;
using CarePath.Infrastructure.ControlPlane.Identity;
using CarePath.Infrastructure.ControlPlane.Persistence;
using CarePath.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarePath.Infrastructure.ControlPlane;

/// <summary>
/// Registers the control-plane context, platform identity, unit of work, options, and audit
/// logger. Called from <see cref="DependencyInjection.AddInfrastructure(IServiceCollection, IConfiguration)"/>.
/// </summary>
public static class ControlPlaneServiceCollectionExtensions
{
    private const string PostgreSqlMigrationsAssembly = "CarePath.Infrastructure.Migrations.PostgreSql";

    /// <summary>
    /// Adds control-plane services. The connection string is
    /// <c>ConnectionStrings:ControlPlane{Provider}Connection</c>, falling back to
    /// <c>ConnectionStrings:ControlPlaneConnection</c>. There is deliberately no fallback to the
    /// tenant connection string: pointing the control plane at the PHI database would silently
    /// recreate the single-database design.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="provider">Database provider shared with the tenant context.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">No control-plane connection string is configured.</exception>
    public static IServiceCollection AddControlPlane(
        this IServiceCollection services,
        IConfiguration configuration,
        DatabaseProvider provider)
    {
        var connectionString = ResolveControlPlaneConnectionString(configuration, provider);
        return services.AddControlPlane(connectionString, provider, configuration.GetSection(ControlPlaneOptions.SectionName));
    }

    /// <summary>Adds control-plane services with an explicit connection string and options section.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="connectionString">Control-plane connection string.</param>
    /// <param name="provider">Database provider.</param>
    /// <param name="optionsSection">The <c>ControlPlane</c> configuration section; may be empty.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddControlPlane(
        this IServiceCollection services,
        string connectionString,
        DatabaseProvider provider,
        IConfiguration optionsSection)
    {
        services.AddOptions<ControlPlaneOptions>()
            .Bind(optionsSection)
            .Validate(
                options => options.BrandingCacheSeconds >= 0 && options.HostDenialAuditWindowSeconds >= 0,
                "ControlPlane:BrandingCacheSeconds and ControlPlane:HostDenialAuditWindowSeconds must be non-negative.")
            .ValidateOnStart();

        var sqlServerMigrationsAssembly = typeof(ControlPlaneDbContext).Assembly.GetName().Name;

        // The scoped context serves the unit of work; the scoped factory gives the audit logger a
        // context of its own so an audit row never joins (or is lost with) a business transaction.
        services.AddDbContext<ControlPlaneDbContext>(ConfigureProvider);
        services.AddDbContextFactory<ControlPlaneDbContext>(ConfigureProvider, ServiceLifetime.Scoped);

        void ConfigureProvider(DbContextOptionsBuilder options)
        {
            switch (provider)
            {
                case DatabaseProvider.PostgreSql:
                    options.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(PostgreSqlMigrationsAssembly);
                        npgsqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(5),
                            errorCodesToAdd: null);
                    });
                    break;

                case DatabaseProvider.SqlServer:
                default:
                    options.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(sqlServerMigrationsAssembly);
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(5),
                            errorNumbersToAdd: null);
                    });
                    break;
            }
        }

        // Identity core only: no roles (D-CP04-04), no SignInManager (no cookie scheme dependency).
        services
            .AddIdentityCore<PlatformUser>(options =>
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
            .AddEntityFrameworkStores<ControlPlaneDbContext>()
            .AddDefaultTokenProviders();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<HostDenialAuditThrottle>();
        services.AddScoped<IControlPlaneUnitOfWork, ControlPlaneUnitOfWork>();
        services.AddScoped<IPlatformAuditLogger, EfPlatformAuditLogger>();
        services.AddScoped<IOrganizationHostResolver, EfOrganizationHostResolver>();
        services.AddScoped<IIdentityService, PlatformIdentityService>();
        services.AddScoped<IIdentityProvisioningService, PlatformIdentityProvisioningService>();
        services.AddScoped<ControlPlaneBootstrapper>();
        services.AddMemoryCache();
        services.AddSingleton<IBrandingCache, MemoryBrandingCache>();

        return services;
    }

    private static string ResolveControlPlaneConnectionString(IConfiguration configuration, DatabaseProvider provider)
    {
        var connectionString = FirstNonBlank(
            configuration.GetConnectionString($"ControlPlane{provider}Connection"),
            configuration.GetConnectionString("ControlPlaneConnection"))
            ?? throw new InvalidOperationException(
                $"Connection string 'ControlPlane{provider}Connection' (or 'ControlPlaneConnection') not found. The control plane never falls back to the tenant connection string.");

        // Same server is fine; the same database is not: both contexts would own AspNetUsers and
        // the physical separation ADR 0003 requires would be gone.
        var tenantConnectionString = FirstNonBlank(
            configuration.GetConnectionString($"{provider}Connection"),
            configuration.GetConnectionString("DefaultConnection"));
        if (tenantConnectionString is not null
            && string.Equals(Normalize(tenantConnectionString), Normalize(connectionString), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The control-plane connection string must not equal the tenant connection string; the control plane requires a separate database.");
        }

        return connectionString;

        static string? FirstNonBlank(params string?[] candidates) =>
            candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));

        static string Normalize(string value) =>
            string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToLowerInvariant().TrimEnd(';');
    }
}
