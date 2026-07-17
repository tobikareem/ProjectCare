using CarePath.Contracts.Common;
using CarePath.Contracts.Scheduling;

namespace CarePath.Application.Scheduling.Services;

public interface IAssignmentHistoryService
{
    Task<PagedResult<CaregiverAssignmentSummaryDto>> GetCaregiversForClientAsync(Guid clientId, AssignmentHistorySearchRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<ClientAssignmentSummaryDto>> GetClientsForCaregiverAsync(Guid caregiverId, AssignmentHistorySearchRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<MyClientAssignmentSummaryDto>> GetMyClientsAsync(AssignmentHistorySearchRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<MyCaregiverAssignmentSummaryDto>> GetMyCaregiversAsync(AssignmentHistorySearchRequest request, CancellationToken cancellationToken = default);
}
