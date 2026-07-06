using System.Globalization;

namespace CarePath.Client.Http;

/// <summary>
/// Query-string value formatting shared by the typed clients.
/// </summary>
internal static class QueryFormat
{
    /// <summary>Formats a UTC timestamp as an URL-encoded ISO 8601 round-trip value.</summary>
    /// <param name="value">The UTC timestamp.</param>
    /// <returns>The encoded value.</returns>
    internal static string Utc(DateTime value) =>
        Uri.EscapeDataString(value.ToString("O", CultureInfo.InvariantCulture));
}
