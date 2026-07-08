using CarePath.Client.Http;
using CarePath.Contracts.Common;
using CarePath.Contracts.Identity;
using CarePath.Contracts.Scheduling;

namespace CarePath.Client.Api;

/// <summary>
/// Typed client for <c>/api/caregivers</c>. Route shapes mirror the Sprint 4 endpoint matrix;
/// authorization is enforced server-side.
/// </summary>
public sealed class CaregiversClient : ApiClientBase
{
    /// <summary>Creates the client.</summary>
    /// <param name="httpClient">HTTP client configured with the API base address.</param>
    public CaregiversClient(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>Gets a page of caregivers.</summary>
    /// <param name="paging">Paging parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The paged caregiver list.</returns>
    public Task<ApiResponse<PagedResult<CaregiverSummaryDto>>> GetPageAsync(
        PagedRequest paging,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<CaregiverSummaryDto>>(
            $"api/caregivers?{paging.ToQueryString()}", cancellationToken);

    /// <summary>Gets a caregiver by ID.</summary>
    /// <param name="caregiverId">Caregiver identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The caregiver detail.</returns>
    public Task<ApiResponse<CaregiverDetailDto>> GetAsync(
        Guid caregiverId,
        CancellationToken cancellationToken = default) =>
        GetAsync<CaregiverDetailDto>($"api/caregivers/{caregiverId}", cancellationToken);

    /// <summary>Creates a caregiver (Admin/Coordinator).</summary>
    /// <param name="request">The create request. Never logged client-side.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created caregiver detail.</returns>
    public Task<ApiResponse<CaregiverDetailDto>> CreateAsync(
        CreateCaregiverRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateCaregiverRequest, CaregiverDetailDto>(
            "api/caregivers", request, cancellationToken);

    /// <summary>Updates a caregiver profile (Admin/Coordinator).</summary>
    /// <param name="caregiverId">Caregiver identifier.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated caregiver detail.</returns>
    public Task<ApiResponse<CaregiverDetailDto>> UpdateAsync(
        Guid caregiverId,
        UpdateCaregiverRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<UpdateCaregiverRequest, CaregiverDetailDto>(
            $"api/caregivers/{caregiverId}", request, cancellationToken);

    /// <summary>Adds a certification to a caregiver (Admin/Coordinator).</summary>
    /// <param name="caregiverId">Caregiver identifier.</param>
    /// <param name="request">The certification request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created certification.</returns>
    public Task<ApiResponse<CertificationDto>> AddCertificationAsync(
        Guid caregiverId,
        AddCertificationRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<AddCertificationRequest, CertificationDto>(
            $"api/caregivers/{caregiverId}/certifications", request, cancellationToken);

    /// <summary>Gets a page of expiring certifications (Admin/Coordinator).</summary>
    /// <param name="paging">Paging parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The paged certification list.</returns>
    public Task<ApiResponse<PagedResult<CertificationDto>>> GetExpiringCertificationsAsync(
        PagedRequest paging,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<CertificationDto>>(
            $"api/caregivers/certifications/expiring?{paging.ToQueryString()}", cancellationToken);

    /// <summary>Gets open shifts evaluated for one caregiver.</summary>
    /// <param name="caregiverId">Caregiver identifier.</param>
    /// <param name="paging">Paging parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The paged eligible open shift list.</returns>
    public Task<ApiResponse<PagedResult<EligibleOpenShiftDto>>> GetEligibleOpenShiftsAsync(
        Guid caregiverId,
        PagedRequest paging,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<EligibleOpenShiftDto>>(
            $"api/caregivers/{caregiverId}/eligible-shifts?{paging.ToQueryString()}", cancellationToken);
}
