using CarePath.Client.Http;
using CarePath.Contracts.Clients;
using CarePath.Contracts.Common;

namespace CarePath.Client.Api;

/// <summary>
/// Typed client for <c>/api/clients</c>, nested care plans, and access grants. Route shapes
/// mirror the Sprint 4 endpoint matrix; authorization and grant evaluation are server-side.
/// </summary>
public sealed class ClientsClient : ApiClientBase
{
    /// <summary>Creates the client.</summary>
    /// <param name="httpClient">HTTP client configured with the API base address.</param>
    public ClientsClient(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>Gets a page of clients (scoped server-side per role).</summary>
    /// <param name="paging">Paging parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The paged client list.</returns>
    public Task<ApiResponse<PagedResult<ClientSummaryDto>>> GetPageAsync(
        PagedRequest paging,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<ClientSummaryDto>>($"api/clients?{paging.ToQueryString()}", cancellationToken);

    /// <summary>Gets a client by ID (PHI — server audits every read).</summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The client detail.</returns>
    public Task<ApiResponse<ClientDetailDto>> GetAsync(
        Guid clientId,
        CancellationToken cancellationToken = default) =>
        GetAsync<ClientDetailDto>($"api/clients/{clientId}", cancellationToken);

    /// <summary>Creates a client (Admin/Coordinator).</summary>
    /// <param name="request">The create request. Never logged client-side.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created client detail.</returns>
    public Task<ApiResponse<ClientDetailDto>> CreateAsync(
        CreateClientRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateClientRequest, ClientDetailDto>("api/clients", request, cancellationToken);

    /// <summary>Updates a client profile (Admin/Coordinator).</summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated client detail.</returns>
    public Task<ApiResponse<ClientDetailDto>> UpdateAsync(
        Guid clientId,
        UpdateClientRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<UpdateClientRequest, ClientDetailDto>($"api/clients/{clientId}", request, cancellationToken);

    /// <summary>Gets a page of a client's care plans.</summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="paging">Paging parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The paged care plan list.</returns>
    public Task<ApiResponse<PagedResult<CarePlanDto>>> GetCarePlansAsync(
        Guid clientId,
        PagedRequest paging,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<CarePlanDto>>(
            $"api/clients/{clientId}/care-plans?{paging.ToQueryString()}", cancellationToken);

    /// <summary>Creates a care plan for a client (Admin/Coordinator/Clinician).</summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="request">The care plan request. Never logged client-side.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created care plan.</returns>
    public Task<ApiResponse<CarePlanDto>> CreateCarePlanAsync(
        Guid clientId,
        CreateCarePlanRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateCarePlanRequest, CarePlanDto>(
            $"api/clients/{clientId}/care-plans", request, cancellationToken);

    /// <summary>Updates a care plan (Admin/Coordinator/Clinician).</summary>
    /// <param name="carePlanId">Care plan identifier.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated care plan.</returns>
    public Task<ApiResponse<CarePlanDto>> UpdateCarePlanAsync(
        Guid carePlanId,
        UpdateCarePlanRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<UpdateCarePlanRequest, CarePlanDto>($"api/care-plans/{carePlanId}", request, cancellationToken);

    /// <summary>Lists a client's access grants (Admin/Coordinator).</summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The grant list.</returns>
    public Task<ApiResponse<IReadOnlyList<ClientAccessGrantDto>>> GetAccessGrantsAsync(
        Guid clientId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<ClientAccessGrantDto>>(
            $"api/clients/{clientId}/access-grants", cancellationToken);

    /// <summary>Grants a family proxy access to a client (Admin/Coordinator, D-S4-1).</summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="request">The grant request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created grant.</returns>
    public Task<ApiResponse<ClientAccessGrantDto>> CreateAccessGrantAsync(
        Guid clientId,
        CreateGrantRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateGrantRequest, ClientAccessGrantDto>(
            $"api/clients/{clientId}/access-grants", request, cancellationToken);

    /// <summary>Revokes an access grant (soft revoke server-side; Admin/Coordinator).</summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="grantId">Grant identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The response envelope.</returns>
    public Task<ApiResponse> RevokeAccessGrantAsync(
        Guid clientId,
        Guid grantId,
        CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/clients/{clientId}/access-grants/{grantId}", cancellationToken);
}
