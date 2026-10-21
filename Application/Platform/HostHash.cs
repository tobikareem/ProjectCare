using System.Security.Cryptography;
using System.Text;

namespace CarePath.Application.Platform;

/// <summary>
/// Pseudonymous host fingerprint for audit rows: lower-case hex SHA-256 of the normalized host.
/// The host string itself is never stored or logged.
/// </summary>
public static class HostHash
{
    /// <summary>Computes the fingerprint of an already-normalized host.</summary>
    /// <param name="normalizedHost">Host normalized by <see cref="HostNameNormalizer"/>.</param>
    /// <returns>64 lower-case hex characters.</returns>
    public static string Compute(string normalizedHost)
    {
        ArgumentNullException.ThrowIfNull(normalizedHost);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedHost)));
    }
}
