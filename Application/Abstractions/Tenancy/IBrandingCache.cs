using CarePath.Contracts.Organization;

namespace CarePath.Application.Abstractions.Tenancy;

/// <summary>
/// Cache for the anonymous branding payload. Presentation data only: nothing read through this
/// cache may influence authentication, authorization, or routing.
/// </summary>
public interface IBrandingCache
{
    /// <summary>Returns the cached payload for the organization, or builds and caches it.</summary>
    /// <param name="organizationId">Organization identifier.</param>
    /// <param name="factory">Builds the payload on a miss.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    Task<OrganizationBrandingDto> GetOrCreateAsync(
        Guid organizationId,
        Func<CancellationToken, Task<OrganizationBrandingDto>> factory,
        CancellationToken cancellationToken = default);

    /// <summary>Drops the cached payload for the organization after a branding or organization change.</summary>
    /// <param name="organizationId">Organization identifier.</param>
    void Invalidate(Guid organizationId);
}
