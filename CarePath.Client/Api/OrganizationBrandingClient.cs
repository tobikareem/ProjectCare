using CarePath.Contracts.Common;
using CarePath.Contracts.Organization;

namespace CarePath.Client.Api;

/// <summary>
/// Typed client for the anonymous <c>/api/organization/branding</c> endpoint (CP-04 FR-012).
/// The server resolves the organization from the request host; the client sends nothing that
/// identifies an organization. A 404 means the host is not a known organization.
/// </summary>
public sealed class OrganizationBrandingClient : ApiClientBase
{
    /// <summary>Creates the client.</summary>
    /// <param name="httpClient">HTTP client configured with the API base address.</param>
    public OrganizationBrandingClient(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>Gets the approved public branding of the organization the current host resolves to.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The branding on success; <c>resource.not_found</c> for an unknown host.</returns>
    public Task<ApiResponse<OrganizationBrandingDto>> GetAsync(CancellationToken cancellationToken = default) =>
        GetAsync<OrganizationBrandingDto>("api/organization/branding", cancellationToken);
}
