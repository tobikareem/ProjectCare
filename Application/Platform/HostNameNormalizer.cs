namespace CarePath.Application.Platform;

/// <summary>
/// Canonical host-name normalization shared by host resolution, validators, bootstrap, and
/// seeding so every component compares the same string.
/// </summary>
public static class HostNameNormalizer
{
    /// <summary>Maximum normalized host length (RFC 1123).</summary>
    public const int MaxLength = 253;

    private const int MaxLabelLength = 63;

    /// <summary>
    /// Normalizes a host: trims, strips a <c>:port</c> suffix, strips one trailing dot, and
    /// lower-cases invariantly. Returns <c>null</c> when the result is empty, non-ASCII,
    /// longer than <see cref="MaxLength"/>, or contains an empty or over-long label.
    /// </summary>
    /// <param name="host">Raw host header value.</param>
    /// <returns>The normalized host, or <c>null</c> when it is not a usable host name.</returns>
    public static string? Normalize(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var value = host.Trim();

        // IPv6 literals are never organization hosts; reject rather than mis-parse the colons.
        if (value.StartsWith('['))
        {
            return null;
        }

        var portIndex = value.IndexOf(':');
        if (portIndex >= 0)
        {
            value = value[..portIndex];
        }

        if (value.EndsWith('.'))
        {
            value = value[..^1];
        }

        value = value.ToLowerInvariant();

        if (value.Length == 0 || value.Length > MaxLength)
        {
            return null;
        }

        foreach (var label in value.Split('.'))
        {
            if (label.Length == 0 || label.Length > MaxLabelLength)
            {
                return null;
            }

            foreach (var character in label)
            {
                if (!IsAllowed(character))
                {
                    return null;
                }
            }

            if (label[0] == '-' || label[^1] == '-')
            {
                return null;
            }
        }

        return value;
    }

    private static bool IsAllowed(char character) =>
        character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-';
}
