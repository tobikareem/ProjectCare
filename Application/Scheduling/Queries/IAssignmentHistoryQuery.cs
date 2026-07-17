using CarePath.Contracts.Common;
using CarePath.Contracts.Scheduling;

namespace CarePath.Application.Scheduling.Queries;

public interface IAssignmentHistoryQuery
{
    Task<PagedResult<CaregiverAssignmentSummaryDto>> GetCaregiversForClientAsync(Guid clientId, AssignmentHistorySearchRequest request, DateTime utcNow, CancellationToken cancellationToken = default);

    Task<PagedResult<ClientAssignmentSummaryDto>> GetClientsForCaregiverAsync(Guid caregiverId, AssignmentHistorySearchRequest request, DateTime utcNow, CancellationToken cancellationToken = default);
}
