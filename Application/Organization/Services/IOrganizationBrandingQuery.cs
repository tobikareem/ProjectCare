using CarePath.Contracts.Organization;

namespace CarePath.Application.Organization.Services;

/// <summary>Anonymous branding read for the organization the current request resolved to.</summary>
public interface IOrganizationBrandingQuery
{
    /// <summary>
    /// Returns the approved branding payload for the resolved tenant.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <exception cref="Common.Exceptions.ResourceNotFoundException">The request host did not resolve to a tenant.</exception>
    Task<OrganizationBrandingDto> GetForCurrentHostAsync(CancellationToken cancellationToken = default);
}
