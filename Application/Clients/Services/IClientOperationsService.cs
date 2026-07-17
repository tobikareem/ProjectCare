using CarePath.Contracts.Clients;
using CarePath.Contracts.Common;

namespace CarePath.Application.Clients.Services;

public interface IClientOperationsService
{
    Task<ClientDetailDto> CreateClientAsync(
        CreateClientRequest request,
        CancellationToken cancellationToken = default);

    Task<ClientDetailDto> UpdateClientAsync(
        Guid clientId,
        UpdateClientRequest request,
        CancellationToken cancellationToken = default);

    Task<ClientDetailDto?> GetClientAsync(
        Guid clientId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ClientSummaryDto>> GetClientsAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<CarePlanDto> CreateCarePlanAsync(
        Guid clientId,
        CreateCarePlanRequest request,
        CancellationToken cancellationToken = default);

    Task<CarePlanDto> UpdateCarePlanAsync(
        Guid carePlanId,
        UpdateCarePlanRequest request,
        CancellationToken cancellationToken = default);

    Task<CarePlanDto> GetCarePlanAsync(
        Guid carePlanId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<CarePlanSummaryDto>> GetCarePlansAsync(
        Guid clientId,
        PagedRequest request,
        CancellationToken cancellationToken = default);
}
