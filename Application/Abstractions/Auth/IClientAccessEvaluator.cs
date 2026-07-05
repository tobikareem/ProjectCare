using CarePath.Domain.Enumerations;

namespace CarePath.Application.Abstractions.Auth;

public interface IClientAccessEvaluator
{
    Task<ClientAccessEvaluationResult> EvaluateAsync(
        Guid granteeUserId,
        Guid clientId,
        AccessScope requiredScope,
        CancellationToken cancellationToken = default);
}
