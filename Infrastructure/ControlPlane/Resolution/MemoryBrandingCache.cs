using CarePath.Application.Abstractions.Tenancy;
using CarePath.Contracts.Organization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CarePath.Infrastructure.ControlPlane.Resolution;

/// <summary>
/// In-memory cache for the public branding payload only. Branding is presentation data, so a
/// bounded staleness is acceptable (ADR 0003 §8.6); it never participates in any security decision.
/// </summary>
public sealed class MemoryBrandingCache : IBrandingCache
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    /// <summary>Initializes the cache.</summary>
    /// <param name="cache">Process-local memory cache.</param>
    /// <param name="options">Control-plane options (TTL).</param>
    public MemoryBrandingCache(IMemoryCache cache, IOptions<ControlPlaneOptions> options)
    {
        _cache = cache;
        _ttl = TimeSpan.FromSeconds(Math.Max(0, options.Value.BrandingCacheSeconds));
    }

    /// <inheritdoc />
    public async Task<OrganizationBrandingDto> GetOrCreateAsync(
        Guid organizationId,
        Func<CancellationToken, Task<OrganizationBrandingDto>> factory,
        CancellationToken cancellationToken = default)
    {
        if (_ttl == TimeSpan.Zero)
        {
            return await factory(cancellationToken);
        }

        if (_cache.TryGetValue(Key(organizationId), out OrganizationBrandingDto? cached) && cached is not null)
        {
            return cached;
        }

        var branding = await factory(cancellationToken);
        _cache.Set(Key(organizationId), branding, _ttl);
        return branding;
    }

    /// <inheritdoc />
    public void Invalidate(Guid organizationId)
    {
        _cache.Remove(Key(organizationId));
    }

    private static string Key(Guid organizationId) => $"branding:{organizationId:N}";
}
