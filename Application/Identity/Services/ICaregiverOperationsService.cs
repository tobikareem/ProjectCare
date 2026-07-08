using CarePath.Contracts.Common;
using CarePath.Contracts.Identity;
using CarePath.Contracts.Scheduling;

namespace CarePath.Application.Identity.Services;

public interface ICaregiverOperationsService
{
    Task<CaregiverDetailDto> CreateCaregiverAsync(
        CreateCaregiverRequest request,
        CancellationToken cancellationToken = default);

    Task<CaregiverDetailDto> UpdateCaregiverAsync(
        Guid caregiverId,
        UpdateCaregiverRequest request,
        CancellationToken cancellationToken = default);

    Task<CaregiverDetailDto> GetCaregiverAsync(
        Guid caregiverId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<CaregiverSummaryDto>> GetCaregiversAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<CertificationDto> AddCertificationAsync(
        Guid caregiverId,
        AddCertificationRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<CertificationDto>> GetExpiringCertificationsAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<EligibleOpenShiftDto>> GetEligibleOpenShiftsAsync(
        Guid caregiverId,
        PagedRequest request,
        CancellationToken cancellationToken = default);
}
