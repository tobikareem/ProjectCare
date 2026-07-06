using CarePath.Contracts.Clients;

namespace CarePath.Application.Clients.Services;

public interface IClientAccessGrantService
{
    Task<IReadOnlyList<ClientAccessGrantDto>> GetGrantsAsync(Guid clientId, CancellationToken cancellationToken = default);

    Task<ClientAccessGrantDto> CreateGrantAsync(Guid clientId, CreateGrantRequest request, CancellationToken cancellationToken = default);

    Task RevokeGrantAsync(Guid clientId, Guid grantId, CancellationToken cancellationToken = default);
}
