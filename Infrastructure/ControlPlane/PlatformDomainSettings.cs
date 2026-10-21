using CarePath.Application.Abstractions.Tenancy;
using CarePath.Application.Platform;
using Microsoft.Extensions.Options;

namespace CarePath.Infrastructure.ControlPlane;

/// <summary>Exposes the configured platform base domain to the Application layer.</summary>
public sealed class PlatformDomainSettings : IPlatformDomainSettings
{
    private readonly IOptions<ControlPlaneOptions> _options;

    /// <summary>Initializes the settings view.</summary>
    public PlatformDomainSettings(IOptions<ControlPlaneOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public string PlatformBaseDomain => _options.Value.PlatformBaseDomain;

    /// <inheritdoc />
    public IReadOnlySet<string> PlatformHostNames =>
        _options.Value.PlatformHostNames
            .Select(HostNameNormalizer.Normalize)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
}
