using CarePath.Client.Http;
using CarePath.Contracts.Admin;
using CarePath.Contracts.Common;
using CarePath.Contracts.Enumerations;

namespace CarePath.Client.Api;

/// <summary>
/// Typed client for <c>/api/admin/users</c> (D-S6-8: role and account-status management).
/// Admin-only server-side; every action is re-verified against the database, not JWT claims.
/// </summary>
public sealed class AdminUsersClient : ApiClientBase
{
    /// <summary>Creates the client.</summary>
    /// <param name="httpClient">HTTP client configured with the API base address.</param>
    public AdminUsersClient(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>Gets a page of user accounts.</summary>
    /// <param name="paging">Paging parameters.</param>
    /// <param name="role">Optional role filter.</param>
    /// <param name="isActive">Optional status filter.</param>
    /// <param name="search">Optional search — matched against email and display name only (never PHI fields).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The paged account list.</returns>
    public Task<ApiResponse<PagedResult<UserAccountDto>>> GetPageAsync(
        PagedRequest paging,
        UserRole? role = null,
        bool? isActive = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var uri = $"api/admin/users?{paging.ToQueryString()}";
        if (role.HasValue)
        {
            uri += $"&role={(int)role.Value}";
        }

        if (isActive.HasValue)
        {
            uri += $"&isActive={(isActive.Value ? "true" : "false")}";
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            uri += $"&search={Uri.EscapeDataString(search)}";
        }

        return GetAsync<PagedResult<UserAccountDto>>(uri, cancellationToken);
    }

    /// <summary>Gets the complete set of assignable application roles.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The role list.</returns>
    public Task<ApiResponse<IReadOnlyList<UserRole>>> GetRolesAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<UserRole>>("api/admin/users/roles", cancellationToken);

    /// <summary>Provisions a staff account (Coordinator/Clinician/FacilityManager/Admin).</summary>
    /// <param name="request">The provisioning request. Never logged client-side.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created account view.</returns>
    public Task<ApiResponse<UserAccountDto>> CreateStaffUserAsync(
        CreateStaffUserRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateStaffUserRequest, UserAccountDto>("api/admin/users", request, cancellationToken);

    /// <summary>Changes a user's role (guardrails enforced server-side; applies at next sign-in).</summary>
    /// <param name="userId">Target user identifier.</param>
    /// <param name="request">The role change.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated account view.</returns>
    public Task<ApiResponse<UserAccountDto>> UpdateRoleAsync(
        Guid userId,
        UpdateUserRoleRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<UpdateUserRoleRequest, UserAccountDto>(
            $"api/admin/users/{userId}/role", request, cancellationToken);

    /// <summary>Activates or deactivates an account (guardrails enforced server-side).</summary>
    /// <param name="userId">Target user identifier.</param>
    /// <param name="request">The status change.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated account view.</returns>
    public Task<ApiResponse<UserAccountDto>> UpdateStatusAsync(
        Guid userId,
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<UpdateUserStatusRequest, UserAccountDto>(
            $"api/admin/users/{userId}/status", request, cancellationToken);
}
