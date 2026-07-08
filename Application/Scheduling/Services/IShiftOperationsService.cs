using CarePath.Contracts.Common;
using CarePath.Contracts.Scheduling;

namespace CarePath.Application.Scheduling.Services;

public interface IShiftOperationsService
{
    Task<ShiftDetailDto> CreateShiftAsync(CreateShiftRequest request, CancellationToken cancellationToken = default);

    Task<ShiftDetailDto> UpdateShiftAsync(Guid shiftId, UpdateShiftRequest request, CancellationToken cancellationToken = default);

    Task CancelShiftAsync(Guid shiftId, string cancellationReason, CancellationToken cancellationToken = default);

    Task<ShiftDetailDto> CheckInAsync(CheckInRequest request, CancellationToken cancellationToken = default);

    Task<ShiftDetailDto> CheckOutAsync(CheckOutRequest request, CancellationToken cancellationToken = default);

    Task<ShiftDetailDto> GetShiftAsync(Guid shiftId, CancellationToken cancellationToken = default);

    Task<PagedResult<ShiftSummaryDto>> GetShiftsAsync(PagedRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<OpenShiftCoverageDto>> GetCoverageQueueAsync(PagedRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<EligibleCaregiverDto>> GetEligibleCaregiversAsync(Guid shiftId, PagedRequest request, CancellationToken cancellationToken = default);
}
