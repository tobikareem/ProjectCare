using CarePath.Application.Abstractions.Auth;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.Auth;

/// <summary>
/// EF Core implementation of client access-grant evaluation.
/// </summary>
public sealed class ClientAccessEvaluator : IClientAccessEvaluator
{
    private readonly CarePathDbContext _context;

    /// <summary>Initializes a new client access evaluator.</summary>
    /// <param name="context">CarePath database context.</param>
    public ClientAccessEvaluator(CarePathDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<ClientAccessEvaluationResult> EvaluateAsync(
        Guid granteeUserId,
        Guid clientId,
        AccessScope requiredScope,
        CancellationToken cancellationToken = default)
    {
        var isClientSelfAccess = await _context.Clients
            .AnyAsync(client => client.Id == clientId && client.UserId == granteeUserId, cancellationToken);

        if (isClientSelfAccess)
        {
            return ClientAccessEvaluationResult.Authorized();
        }

        var activeScopes = await _context.ClientAccessGrants
            .Where(grant => grant.GranteeUserId == granteeUserId)
            .Where(grant => grant.ClientId == clientId)
            .Where(grant => grant.RevokedAtUtc == null)
            .Select(grant => grant.AccessScope)
            .ToListAsync(cancellationToken);

        if (activeScopes.Count == 0)
        {
            return ClientAccessEvaluationResult.Denied(ClientAccessEvaluationResult.NoGrant);
        }

        return activeScopes.Any(scope => Satisfies(scope, requiredScope))
            ? ClientAccessEvaluationResult.Authorized()
            : ClientAccessEvaluationResult.Denied(ClientAccessEvaluationResult.ScopeInsufficient);
    }

    private static bool Satisfies(AccessScope grantedScope, AccessScope requiredScope)
    {
        return grantedScope == AccessScope.Full || grantedScope == requiredScope;
    }
}
