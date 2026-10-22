using CarePath.Application;
using CarePath.Application.Abstractions.Auth;
using CarePath.Infrastructure;
using CarePath.Infrastructure.ControlPlane.Bootstrap;
using CarePath.Infrastructure.ControlPlane.Persistence;
using CarePath.WebApi.Tenancy;
using CarePath.Infrastructure.ControlPlane.Identity;
using CarePath.Infrastructure.Persistence;
using CarePath.WebApi.Diagnostics;
using CarePath.WebApi.Middleware;
using CarePath.WebApi.ModelBinding;
using CarePath.WebApi.OpenApi;
using CarePath.WebApi.Security;
using CarePath.WebApi.Serialization;
using CarePath.WebApi.Validation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

const string webClientCorsPolicy = "WebClient";

// Docker health-check mode: probe the running instance over loopback and exit, without
// building the host. Must stay ahead of CreateBuilder so a probe never touches the database.
if (args.Contains(HealthProbe.Argument, StringComparer.Ordinal))
{
    return await HealthProbe.RunAsync();
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
builder.Services.AddOrganizationResolution();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCarePathAuthentication(builder.Configuration);

// Liveness needs no registered checks; readiness is tagged so the two endpoints below can
// select different subsets of the same registry.
builder.Services
    .AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
    .AddCheck<ControlPlaneHealthCheck>("control-plane", tags: ["ready"]);

// Application-layer validators require DateTimeKind.Utc; normalize every DateTime at the
// HTTP boundary (JSON bodies via the converter, query/route values via the binder) so
// offset-less client dates are treated as UTC instead of failing validation.
builder.Services
    .AddControllers(options =>
    {
        options.ModelBinderProviders.Insert(0, new UtcDateTimeModelBinderProvider());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = InvalidModelStateProblemFactory.Create;
    });

// Browser clients (Blazor WASM) send the JWT in the Authorization header — no cookies, so no
// AllowCredentials. Origins come from configuration and the policy fails closed when unset.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(webClientCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CarePath Health API",
        Version = "v1",
        Description = "Operational API for CarePath Health."
    });

    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste a JWT access token from /api/auth/login."
    });

    options.OperationFilter<AuthorizeOperationFilter>();
});

// Keep the built-in OpenAPI endpoint available for local development tooling.
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseCarePathProblemDetails();

// Forwarded headers are honored only from ControlPlane:TrustedProxies; host resolution below
// reads the resulting Request.Host.
app.UseTrustedForwardedHeaders();

// The WASM client calls the documented http://localhost:5240 dev endpoint; a 307 to https
// breaks browser CORS preflights, so HTTPS is only forced outside Development.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

var swaggerEnabled = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CarePath Health API v1");
        options.DocumentTitle = "CarePath Health API";
        options.RoutePrefix = "swagger";
    });
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Startup database work is configuration-driven rather than environment-gated, so any
// environment (Development, HomeLab, ...) reports which provider it actually resolved.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CarePathDbContext>();
    var controlPlane = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CarePath.Startup");

    // Connection-only probe: opens a connection and closes it. Creates and alters nothing.
    var canConnect = await context.Database.CanConnectAsync();
    startupLogger.LogInformation(
        "Database connectivity check via {Provider}: {Result}",
        context.Database.ProviderName,
        canConnect ? "SUCCEEDED" : "FAILED");

    // CP-04 startup sequence (design spec, "Startup Sequence"): the control plane migrates
    // first, legacy identities are copied before the tenant migration drops them from the model,
    // and the idempotent backfill steps run on every startup regardless of AutoMigrate.
    var autoMigrate = app.Configuration.GetValue<bool>("Database:AutoMigrate");
    var bootstrapper = scope.ServiceProvider.GetRequiredService<ControlPlaneBootstrapper>();

    if (autoMigrate)
    {
        await controlPlane.Database.MigrateAsync();
    }

    await bootstrapper.CopyLegacyIdentityAsync();

    if (autoMigrate)
    {
        await context.Database.MigrateAsync();
    }

    var defaultOrganizationId = await bootstrapper.EnsureDefaultOrganizationAsync();
    if (defaultOrganizationId is not null)
    {
        await bootstrapper.EnsureMembershipsAsync(defaultOrganizationId.Value);
    }

    await bootstrapper.EnsurePlatformAdminAsync();

    // One operational database until CP-06: a second Active organization would share the tenant
    // database, so refuse to start unless ControlPlane:AllowMultipleActiveOrganizations says this
    // deployment holds synthetic data only (Development sets it for the seeded second organization).
    await bootstrapper.EnsureSingleOperationalOrganizationAsync();

    if (autoMigrate)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        await CarePathDbContextSeed.SeedAsync(context, controlPlane, userManager, builder.Configuration, app.Environment);
    }
}

app.UseCors(webClientCorsPolicy);

// Host -> organization before authentication, so a token can be checked against the host it was
// presented at and an unknown host never reaches authentication or a tenant query.
app.UseOrganizationResolution();

app.UseAuthentication();
app.UseAuthorization();

// AllowAnonymous is required: the fallback authorization policy would otherwise answer both
// endpoints with 401. Neither returns PHI — the default writer emits only the status word.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
}).AllowAnonymous();

app.MapControllers();

app.Run();

return 0;
