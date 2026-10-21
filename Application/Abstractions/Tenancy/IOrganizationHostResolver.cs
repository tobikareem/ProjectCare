namespace CarePath.Application.Abstractions.Tenancy;

/// <summary>
/// Resolves a normalized request host to an admissible organization with one authoritative
/// control-plane read per call (D-CP04-03). Implementations must not cache status.
/// </summary>
public interface IOrganizationHostResolver
{
    /// <summary>Resolves the host.</summary>
    /// <param name="normalizedHost">Host already normalized by <c>HostNameNormalizer</c>.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>The resolution outcome. Infrastructure failures propagate as exceptions so the caller can fail closed.</returns>
    Task<HostResolution> ResolveAsync(string normalizedHost, CancellationToken cancellationToken = default);
}
