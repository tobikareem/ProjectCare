using CarePath.Client.Http;
using CarePath.Contracts.Common;
using CarePath.Contracts.Platform;

namespace CarePath.Client.Api;

/// <summary>
/// Typed client for <c>/api/platform/organizations</c> (CP-04 FR-014). Requires a platform-kind
/// session at the platform host; a tenant session is rejected server-side.
/// </summary>
public sealed class PlatformOrganizationsClient : ApiClientBase
{
    private const string BasePath = "api/platform/organizations";

    /// <summary>Creates the client.</summary>
    /// <param name="httpClient">HTTP client configured with the API base address.</param>
    public PlatformOrganizationsClient(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>Gets a page of organizations.</summary>
    /// <param name="paging">Paging parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The paged organization list.</returns>
    public Task<ApiResponse<PagedResult<OrganizationSummaryDto>>> GetPageAsync(
        PagedRequest paging,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<OrganizationSummaryDto>>($"{BasePath}?{paging.ToQueryString()}", cancellationToken);

    /// <summary>Registers an organization (Pending / Registered until CP-06 provisioning).</summary>
    /// <param name="request">The registration request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created organization.</returns>
    public Task<ApiResponse<OrganizationDto>> RegisterAsync(
        RegisterOrganizationRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<RegisterOrganizationRequest, OrganizationDto>(BasePath, request, cancellationToken);

    /// <summary>Gets an organization.</summary>
    /// <param name="organizationId">Organization identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The organization.</returns>
    public Task<ApiResponse<OrganizationDto>> GetAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default) =>
        GetAsync<OrganizationDto>($"{BasePath}/{organizationId}", cancellationToken);

    /// <summary>Updates names, time zone, and region.</summary>
    /// <param name="organizationId">Organization identifier.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated organization.</returns>
    public Task<ApiResponse<OrganizationDto>> UpdateAsync(
        Guid organizationId,
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<UpdateOrganizationRequest, OrganizationDto>($"{BasePath}/{organizationId}", request, cancellationToken);

    /// <summary>Suspends an organization; its sessions are rejected immediately.</summary>
    /// <param name="organizationId">Organization identifier.</param>
    /// <param name="request">The suspension reason.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated organization.</returns>
    public Task<ApiResponse<OrganizationDto>> SuspendAsync(
        Guid organizationId,
        SuspendOrganizationRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SuspendOrganizationRequest, OrganizationDto>($"{BasePath}/{organizationId}/suspend", request, cancellationToken);

    /// <summary>Reactivates a suspended, provisioned organization.</summary>
    /// <param name="organizationId">Organization identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated organization.</returns>
    public Task<ApiResponse<OrganizationDto>> ReactivateAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default) =>
        PostAsync<OrganizationDto>($"{BasePath}/{organizationId}/reactivate", cancellationToken);

    /// <summary>Lists an organization's host names.</summary>
    /// <param name="organizationId">Organization identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The domain list.</returns>
    public Task<ApiResponse<IReadOnlyList<OrganizationDomainDto>>> GetDomainsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<OrganizationDomainDto>>($"{BasePath}/{organizationId}/domains", cancellationToken);

    /// <summary>Adds a host name.</summary>
    /// <param name="organizationId">Organization identifier.</param>
    /// <param name="request">The domain request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created domain.</returns>
    public Task<ApiResponse<OrganizationDomainDto>> AddDomainAsync(
        Guid organizationId,
        AddOrganizationDomainRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<AddOrganizationDomainRequest, OrganizationDomainDto>($"{BasePath}/{organizationId}/domains", request, cancellationToken);

    /// <summary>Removes a host name.</summary>
    /// <param name="organizationId">Organization identifier.</param>
    /// <param name="domainId">Domain identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Success or the mapped failure.</returns>
    public Task<ApiResponse> RemoveDomainAsync(
        Guid organizationId,
        Guid domainId,
        CancellationToken cancellationToken = default) =>
        DeleteAsync($"{BasePath}/{organizationId}/domains/{domainId}", cancellationToken);

    /// <summary>Gets an organization's branding.</summary>
    /// <param name="organizationId">Organization identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The branding.</returns>
    public Task<ApiResponse<OrganizationBrandingDetailDto>> GetBrandingAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default) =>
        GetAsync<OrganizationBrandingDetailDto>($"{BasePath}/{organizationId}/branding", cancellationToken);

    /// <summary>Replaces an organization's branding.</summary>
    /// <param name="organizationId">Organization identifier.</param>
    /// <param name="request">The branding request (color tokens only — never raw colors).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated branding.</returns>
    public Task<ApiResponse<OrganizationBrandingDetailDto>> UpdateBrandingAsync(
        Guid organizationId,
        UpdateOrganizationBrandingRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<UpdateOrganizationBrandingRequest, OrganizationBrandingDetailDto>($"{BasePath}/{organizationId}/branding", request, cancellationToken);
}
