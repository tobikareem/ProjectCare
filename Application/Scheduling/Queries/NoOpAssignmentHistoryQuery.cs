using CarePath.Contracts.Common;
using CarePath.Contracts.Scheduling;

namespace CarePath.Application.Scheduling.Queries;

/// <summary>Safe fallback used when Infrastructure persistence is not registered.</summary>
public sealed class NoOpAssignmentHistoryQuery : IAssignmentHistoryQuery
{
    public Task<PagedResult<CaregiverAssignmentSummaryDto>> GetCaregiversForClientAsync(Guid clientId, AssignmentHistorySearchRequest request, DateTime utcNow, CancellationToken cancellationToken = default) =>
        Task.FromResult(Empty<CaregiverAssignmentSummaryDto>(request));

    public Task<PagedResult<ClientAssignmentSummaryDto>> GetClientsForCaregiverAsync(Guid caregiverId, AssignmentHistorySearchRequest request, DateTime utcNow, CancellationToken cancellationToken = default) =>
        Task.FromResult(Empty<ClientAssignmentSummaryDto>(request));

    private static PagedResult<T> Empty<T>(AssignmentHistorySearchRequest request) => new()
    {
        Items = [],
        PageNumber = request.PageNumber,
        PageSize = request.PageSize,
        TotalCount = 0,
    };
}
