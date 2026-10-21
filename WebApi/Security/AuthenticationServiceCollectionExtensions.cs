using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
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
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = JwtTokenValidationParametersFactory.Create(configuration);
                options.Events = new JwtBearerEvents
                {
                    // Token <-> host binding (CP-04 FR-009/FR-010/FR-013): a valid signature is not
                    // enough; the token must have been issued for the host it is presented at.
                    OnTokenValidated = context =>
                    {
                        var organization = context.HttpContext.RequestServices.GetRequiredService<IOrganizationContext>();
                        if (!TokenMatchesHost(context.Principal, organization))
                        {
                            context.Fail("token_binding");
                        }

                        return Task.CompletedTask;
                    },

                    // Same generic body as login failures and unresolved hosts (requirement §4.4).
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await UnauthorizedResponseWriter.WriteAsync(context.HttpContext);
                    },
                };
            });

        services.AddAuthorization(options =>
        {
            foreach (var role in ApplicationRoles.All)
            {
                options.AddPolicy(role, policy =>
                {
                    policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
                    policy.RequireRole(role);
                    policy.RequireClaim(CarePathClaimTypes.TokenKind, CarePathClaimTypes.TenantTokenKind);
                });
            }

            options.AddPolicy(CarePathAuthorizationPolicies.PlatformAdmin, policy =>
            {
                policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
                policy.RequireRole(ApplicationRoles.PlatformAdmin);
                policy.RequireClaim(CarePathClaimTypes.TokenKind, CarePathClaimTypes.PlatformTokenKind);
            });

            // Defense in depth for [Authorize] / [Authorize(Roles = ...)] endpoints, which use the
            // default policy rather than the named role policies: tenant endpoints accept only
            // tenant-kind tokens even if the OnTokenValidated binding were ever bypassed.
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireClaim(CarePathClaimTypes.TokenKind, CarePathClaimTypes.TenantTokenKind)
                .Build();

            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    private static bool TokenMatchesHost(System.Security.Claims.ClaimsPrincipal? principal, IOrganizationContext organization)
    {
        if (principal is null)
        {
            return false;
        }

        var kind = principal.FindFirst(CarePathClaimTypes.TokenKind)?.Value;
        var organizationClaim = principal.FindFirst(CarePathClaimTypes.OrganizationId)?.Value;
        var membershipClaim = principal.FindFirst(CarePathClaimTypes.MembershipId)?.Value;

        return organization.Kind switch
        {
            OrganizationResolutionKind.Tenant =>
                kind == CarePathClaimTypes.TenantTokenKind
                && Guid.TryParse(organizationClaim, out var tokenOrganization)
                && tokenOrganization == organization.OrganizationId
                && Guid.TryParse(membershipClaim, out _),
            OrganizationResolutionKind.Platform =>
                kind == CarePathClaimTypes.PlatformTokenKind && organizationClaim is null,
            _ => false,
        };
    }
}
