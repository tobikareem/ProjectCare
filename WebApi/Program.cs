using CarePath.Application;
using CarePath.Application.Abstractions.Auth;
using CarePath.Infrastructure;
using CarePath.Infrastructure.Identity;
using CarePath.Infrastructure.Persistence;
using CarePath.WebApi.Middleware;
using CarePath.WebApi.Security;
using CarePath.WebApi.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

const string webClientCorsPolicy = "WebClient";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCarePathAuthentication(builder.Configuration);

builder.Services
    .AddControllers()
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

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseCarePathProblemDetails();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<CarePathDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

    await context.Database.MigrateAsync();
    await CarePathDbContextSeed.SeedAsync(context, userManager, roleManager, builder.Configuration, app.Environment);
}

// The WASM client calls the documented http://localhost:5240 dev endpoint; a 307 to https
// breaks browser CORS preflights, so HTTPS is only forced outside Development.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(webClientCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
