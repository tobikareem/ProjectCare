using CarePath.Contracts.Common;
using CarePath.Contracts.Platform;

namespace CarePath.Application.Platform.Services;

/// <summary>Platform-operator management of organizations, their host names, and branding (CP-04 FR-014/FR-015).</summary>
public interface IPlatformOrganizationService
{
    /// <summary>Registers an organization as Pending / Registered with a verified system subdomain. No database is created.</summary>
    Task<OrganizationDto> RegisterAsync(RegisterOrganizationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Lists organizations.</summary>
    Task<PagedResult<OrganizationSummaryDto>> ListAsync(PagedRequest paging, CancellationToken cancellationToken = default);

    /// <summary>Gets an organization with its domains and data-plane summary.</summary>
    Task<OrganizationDto> GetAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>Updates names, time zone, and region.</summary>
    Task<OrganizationDto> UpdateAsync(Guid organizationId, UpdateOrganizationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Suspends an Active organization. Takes effect on the next request.</summary>
    Task<OrganizationDto> SuspendAsync(Guid organizationId, SuspendOrganizationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reactivates a Suspended organization; only a Provisioned organization can become Active.</summary>
    Task<OrganizationDto> ReactivateAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>Lists host names.</summary>
    Task<IReadOnlyList<OrganizationDomainDto>> GetDomainsAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>Adds a host name. System subdomains verify on creation; other hosts stay Pending.</summary>
    Task<OrganizationDomainDto> AddDomainAsync(Guid organizationId, AddOrganizationDomainRequest request, CancellationToken cancellationToken = default);

    /// <summary>Soft-removes a host name. The last verified host cannot be removed.</summary>
    Task RemoveDomainAsync(Guid organizationId, Guid domainId, CancellationToken cancellationToken = default);

    /// <summary>Gets branding.</summary>
    Task<OrganizationBrandingDetailDto> GetBrandingAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>Replaces branding.</summary>
    Task<OrganizationBrandingDetailDto> UpdateBrandingAsync(Guid organizationId, UpdateOrganizationBrandingRequest request, CancellationToken cancellationToken = default);
}
