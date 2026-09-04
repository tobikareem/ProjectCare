using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Billing;
using CarePath.Application.Abstractions.Storage;
using CarePath.Application.Transitions.Interfaces;
using CarePath.Domain.Interfaces.Repositories;
using CarePath.Infrastructure.Audit;
using CarePath.Infrastructure.Auth;
using CarePath.Infrastructure.Billing;
using CarePath.Infrastructure.Identity;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using CarePath.Infrastructure.Persistence.Repositories;
using CarePath.Infrastructure.Storage;
using CarePath.Infrastructure.Scheduling;
using CarePath.Application.Scheduling.Queries;
using CarePath.Infrastructure.Transitions.Services;
using Microsoft.AspNetCore.DataProtection;
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
    // PostgreSQL migrations live in their own assembly so they never mix with the SQL Server
    // set, which EF would otherwise discover together in CarePath.Infrastructure.
    private const string PostgreSqlMigrationsAssembly = "CarePath.Infrastructure.Migrations.PostgreSql";

    /// <summary>
    /// Adds the Infrastructure layer using the configured database provider and connection string.
    /// </summary>
    /// <param name="services">Service collection to register with.</param>
    /// <param name="configuration">
    /// Application configuration containing the optional <c>Database:Provider</c> value and a
    /// connection string (<c>ConnectionStrings:{Provider}Connection</c>, falling back to
    /// <c>ConnectionStrings:DefaultConnection</c>).
    /// </param>
    /// <returns>The same service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>Database:Provider</c> names an unsupported provider, or when no
    /// connection string is configured for the selected provider.
    /// </exception>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = ResolveProvider(configuration);
        var connectionString = ResolveConnectionString(configuration, provider);

        services.AddInfrastructure(connectionString, provider);
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
    /// Adds the Infrastructure layer using the supplied connection string and database provider.
    /// </summary>
    /// <param name="services">Service collection to register with.</param>
    /// <param name="connectionString">Connection string for the selected provider.</param>
    /// <param name="provider">Database provider to register. Defaults to SQL Server.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        DatabaseProvider provider = DatabaseProvider.SqlServer)
    {
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddScoped<AuditableEntityInterceptor>();

        var migrationsAssembly = typeof(CarePathDbContext).Assembly.GetName().Name;

        services.AddDbContext<CarePathDbContext>(options =>
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
                        sqlOptions.MigrationsAssembly(migrationsAssembly);
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(5),
                            errorNumbersToAdd: null);
                    });
                    break;
            }
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
        services.AddScoped<IIdentityRoleManagementService, IdentityRoleManagementService>();
        services.AddScoped<IPhiAuditLogger, LoggingPhiAuditLogger>();
        services.AddScoped<IFileStorageService, DisabledFileStorageService>();
        services.AddScoped<IShiftBillingQuery, ShiftBillingQuery>();
        services.AddScoped<IBillingEligibilityQuery, BillingEligibilityQuery>();
        services.AddScoped<IBillingReconciliationStore, BillingReconciliationStore>();
        services.AddSingleton<IInvoicePreviewTokenService, InvoicePreviewTokenService>();
        // Stable application name so preview tokens survive across instances once key-ring
        // persistence is configured for the deployment environment (pre-production follow-up).
        services.AddDataProtection().SetApplicationName("CarePath");
        services.AddScoped<IAssignmentHistoryQuery, AssignmentHistoryQuery>();
        // Unique-violation detection is provider-specific; selection follows the same
        // Database:Provider value that chose the DbContext provider above.
        if (provider == DatabaseProvider.PostgreSql)
        {
            services.AddScoped<IPersistenceConflictDetector, PostgreSqlPersistenceConflictDetector>();
        }
        else
        {
            services.AddScoped<IPersistenceConflictDetector, SqlServerPersistenceConflictDetector>();
        }

        services.AddScoped<IDischargeExtractionService, RuleBasedDischargeExtractionService>();

        return services;
    }

    private static DatabaseProvider ResolveProvider(IConfiguration configuration)
    {
        var configured = configuration["Database:Provider"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DatabaseProvider.SqlServer;
        }

        return Enum.TryParse<DatabaseProvider>(configured, ignoreCase: true, out var provider)
            ? provider
            : throw new InvalidOperationException(
                $"Unsupported database provider '{configured}'. Supported values: {string.Join(", ", Enum.GetNames<DatabaseProvider>())}.");
    }

    // The provider-specific name wins so one environment can hold both connection strings and
    // switch with a single Database:Provider value. DefaultConnection remains the fallback for
    // deployments (including production) that define only one connection string.
    private static string ResolveConnectionString(IConfiguration configuration, DatabaseProvider provider)
    {
        return configuration.GetConnectionString($"{provider}Connection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }
}
