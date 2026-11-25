using System.Net;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Infrastructure.ControlPlane;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace CarePath.WebApi.Tenancy;

/// <summary>Registration and pipeline helpers for host-based organization resolution.</summary>
public static class OrganizationResolutionExtensions
{
    /// <summary>Registers the scoped organization context.</summary>
    /// <param name="services">Service collection.</param>
    public static IServiceCollection AddOrganizationResolution(this IServiceCollection services)
    {
        services.AddScoped<OrganizationContext>();
        services.AddScoped<IOrganizationContext>(provider => provider.GetRequiredService<OrganizationContext>());
        return services;
    }

    /// <summary>
    /// Honors <c>X-Forwarded-*</c> only from the proxies listed in <c>ControlPlane:TrustedProxies</c>.
    /// With an empty list the middleware is not registered at all: ForwardedHeadersMiddleware skips
    /// its known-proxy check when both lists are empty, which would mean "trust everyone".
    /// </summary>
    /// <param name="app">Application builder.</param>
    public static IApplicationBuilder UseTrustedForwardedHeaders(this IApplicationBuilder app)
    {
        var trusted = app.ApplicationServices.GetRequiredService<IOptions<ControlPlaneOptions>>().Value.TrustedProxies;
        if (trusted.KnownProxies.Length == 0 && trusted.KnownNetworks.Length == 0)
        {
            return app;
        }

        var forwardedOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
            ForwardLimit = 1,
        };

        // The defaults trust loopback; replace them with the explicit list so a forged header from
        // an unlisted source can never choose the organization.
        forwardedOptions.KnownProxies.Clear();
        forwardedOptions.KnownIPNetworks.Clear();
        try
        {
            foreach (var proxy in trusted.KnownProxies)
            {
                forwardedOptions.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in trusted.KnownNetworks)
            {
                forwardedOptions.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("ControlPlane:TrustedProxies contains an address or CIDR that could not be parsed.", exception);
        }

        return app.UseForwardedHeaders(forwardedOptions);
    }

    /// <summary>Adds the organization resolution middleware. Place after CORS and before authentication.</summary>
    /// <param name="app">Application builder.</param>
    public static IApplicationBuilder UseOrganizationResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<OrganizationResolutionMiddleware>();
}
