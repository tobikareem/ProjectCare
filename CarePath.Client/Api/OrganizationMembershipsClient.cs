using CarePath.Client.Http;
using CarePath.Contracts.Common;
using CarePath.Contracts.Organization;

namespace CarePath.Client.Api;

/// <summary>
/// Typed client for <c>/api/organization/memberships</c> (CP-04 FR-016/FR-017). The organization
/// is always the one the session's host resolved to; this client never sends an organization id.
/// Admin-only server-side; every guardrail is re-verified against the control plane.
/// </summary>
public sealed class OrganizationMembershipsClient : ApiClientBase
{
    /// <summary>Creates the client.</summary>
    /// <param name="httpClient">HTTP client configured with the API base address.</param>
    public OrganizationMembershipsClient(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>Gets a page of memberships for the current organization.</summary>
    /// <param name="paging">Paging parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The paged membership list.</returns>
    public Task<ApiResponse<PagedResult<MembershipDto>>> GetPageAsync(
        PagedRequest paging,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<MembershipDto>>($"api/organization/memberships?{paging.ToQueryString()}", cancellationToken);

    /// <summary>Creates a membership for an existing or new platform user.</summary>
    /// <param name="request">The create request. Never logged client-side.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created membership.</returns>
    public Task<ApiResponse<MembershipDto>> CreateAsync(
        CreateMembershipRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateMembershipRequest, MembershipDto>("api/organization/memberships", request, cancellationToken);

    /// <summary>Changes a membership's role (last-active-Admin guard enforced server-side).</summary>
    /// <param name="membershipId">Target membership identifier.</param>
    /// <param name="request">The role change.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated membership.</returns>
    public Task<ApiResponse<MembershipDto>> UpdateRoleAsync(
        Guid membershipId,
        UpdateMembershipRoleRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<UpdateMembershipRoleRequest, MembershipDto>(
            $"api/organization/memberships/{membershipId}/role", request, cancellationToken);

    /// <summary>Activates or deactivates a membership (guardrails enforced server-side).</summary>
    /// <param name="membershipId">Target membership identifier.</param>
    /// <param name="request">The status change.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated membership.</returns>
    public Task<ApiResponse<MembershipDto>> UpdateStatusAsync(
        Guid membershipId,
        UpdateMembershipStatusRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<UpdateMembershipStatusRequest, MembershipDto>(
            $"api/organization/memberships/{membershipId}/status", request, cancellationToken);
}
