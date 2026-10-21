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
            // Only a single ":<1-5 digits>" suffix is a port; anything else is not a host.
            var port = value[(portIndex + 1)..];
            if (port.Length is 0 or > 5 || !port.All(char.IsAsciiDigit))
            {
                return null;
            }

            value = value[..portIndex];
        }

        if (value.EndsWith('.'))
        {
            value = value[..^1];
        }

        // Validate the character set before lowercasing: invariant lowercasing can fold non-ASCII
        // characters (e.g. the Kelvin sign) into ASCII and manufacture a different valid host.
        if (value.Length == 0 || value.Length > MaxLength || !value.All(char.IsAscii))
        {
            return null;
        }

        value = value.ToLowerInvariant();

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
