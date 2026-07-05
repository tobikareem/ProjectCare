using CarePath.Application.Abstractions.Auth;
using CarePath.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace CarePath.WebApi.Security;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddCarePathAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = JwtTokenValidationParametersFactory.Create(configuration);
            });

        services.AddAuthorization(options =>
        {
            foreach (var role in ApplicationRoles.All)
            {
                options.AddPolicy(role, policy => policy.RequireRole(role));
            }

            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}