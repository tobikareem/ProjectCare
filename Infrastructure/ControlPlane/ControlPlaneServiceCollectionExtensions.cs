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
        var sqlServerMigrationsAssembly = typeof(ControlPlaneDbContext).Assembly.GetName().Name;

        return services.AddControlPlane(
            options =>
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
            },
            optionsSection);
    }

    /// <summary>
    /// Adds control-plane services over a caller-configured context (the production overloads
    /// above, or an in-memory provider in the integration suite).
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configureContext">Configures the <see cref="ControlPlaneDbContext"/> provider.</param>
    /// <param name="optionsSection">The <c>ControlPlane</c> configuration section; may be empty.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddControlPlane(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureContext,
        IConfiguration optionsSection)
    {
        services.AddOptions<ControlPlaneOptions>()
            .Bind(optionsSection)
            .Validate(
                options => options.BrandingCacheSeconds >= 0 && options.HostDenialAuditWindowSeconds >= 0,
                "ControlPlane:BrandingCacheSeconds and ControlPlane:HostDenialAuditWindowSeconds must be non-negative.")
            .ValidateOnStart();

        // The scoped context serves the unit of work; the scoped factory gives the audit logger a
        // context of its own so an audit row never joins (or is lost with) a business transaction.
        services.AddDbContext<ControlPlaneDbContext>(configureContext);
        services.AddDbContextFactory<ControlPlaneDbContext>(configureContext, ServiceLifetime.Scoped);

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
        services.AddScoped<IPlatformUserDirectory, PlatformUserDirectory>();
        services.AddSingleton<IPlatformDomainSettings, PlatformDomainSettings>();
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
        if (tenantConnectionString is not null && TargetsSameDatabase(tenantConnectionString, connectionString))
        {
            throw new InvalidOperationException(
                "The control-plane connection string must not equal the tenant connection string; the control plane requires a separate database.");
        }

        return connectionString;

        static string? FirstNonBlank(params string?[] candidates) =>
            candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
    }

    // Keys SQL Server (Microsoft.Data.SqlClient) and Npgsql accept for the server and catalog.
    private static readonly string[] ServerKeys = ["server", "data source", "datasource", "host", "addr", "address", "network address"];
    private static readonly string[] DatabaseKeys = ["database", "initial catalog"];

    /// <summary>
    /// True when both strings name the same server and database, whatever the key order,
    /// synonyms, credentials or optional parameters. Falls back to whitespace/case-insensitive
    /// text equality when either string cannot be parsed.
    /// </summary>
    public static bool TargetsSameDatabase(string first, string second)
    {
        var firstTarget = DatabaseTarget(first);
        var secondTarget = DatabaseTarget(second);
        if (firstTarget is not null && secondTarget is not null)
        {
            // A missing catalog means "the login's default database": treated as the same target
            // when the servers match, because refusing is the safe direction for this guard.
            return string.Equals(firstTarget.Value.Server, secondTarget.Value.Server, StringComparison.OrdinalIgnoreCase)
                && (firstTarget.Value.Database is null
                    || secondTarget.Value.Database is null
                    || string.Equals(firstTarget.Value.Database, secondTarget.Value.Database, StringComparison.OrdinalIgnoreCase));
        }

        static string Collapse(string value) =>
            string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToLowerInvariant().TrimEnd(';');
        return string.Equals(Collapse(first), Collapse(second), StringComparison.Ordinal);
    }

    private static (string Server, string? Database)? DatabaseTarget(string connectionString)
    {
        try
        {
            var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connectionString };
            var server = ServerKeys.Select(key => builder.TryGetValue(key, out var value) ? value?.ToString() : null).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            var database = DatabaseKeys.Select(key => builder.TryGetValue(key, out var value) ? value?.ToString() : null).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            return server is null ? null : (CanonicalServer(server), database?.Trim());
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // "tcp:host,1433", "host:5432", "HOST.", ".", "(local)", "127.0.0.1" and "localhost" all name
    // the same machine for this guard; the port is dropped because one server process hosts both
    // catalogs whatever port the string spells.
    private static string CanonicalServer(string server)
    {
        var value = server.Trim().ToLowerInvariant();
        if (value.StartsWith("tcp:", StringComparison.Ordinal))
        {
            value = value[4..];
        }

        var instanceIndex = value.IndexOf('\\');
        var instance = instanceIndex >= 0 ? value[instanceIndex..] : string.Empty;
        var host = instanceIndex >= 0 ? value[..instanceIndex] : value;
        var portIndex = host.IndexOfAny([',', ':']);
        if (portIndex >= 0 && host[(portIndex + 1)..].All(char.IsAsciiDigit))
        {
            host = host[..portIndex];
        }

        host = host.TrimEnd('.');
        if (host is "." or "(local)" or "127.0.0.1" or "::1" or "[::1]")
        {
            host = "localhost";
        }

        return host + instance;
    }
}
