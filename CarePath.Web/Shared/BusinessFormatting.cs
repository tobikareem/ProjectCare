using System.Globalization;

namespace CarePath.Web.Shared;

/// <summary>
/// Invariant display formatting for the Business pages (Billing, Analytics, Compliance) so
/// monetary and date values render like the wireframe regardless of browser culture.
/// </summary>
internal static class BusinessFormatting
{
    /// <summary>Formats a monetary value as "$1,234.56" (or "-$1,234.56").</summary>
    public static string Money(decimal value) =>
        value < 0
            ? string.Create(CultureInfo.InvariantCulture, $"-${Math.Abs(value):N2}")
            : string.Create(CultureInfo.InvariantCulture, $"${value:N2}");

    /// <summary>Formats a percentage value as "42.1%".</summary>
    public static string Percent(decimal value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value:N1}%");

    /// <summary>Formats an hours value as "3,248.5".</summary>
    public static string Hours(decimal value) =>
        value.ToString("N1", CultureInfo.InvariantCulture);

    /// <summary>Formats a date as "Jun 30" (wireframe billing-row style).</summary>
    public static string ShortDate(DateTime value) =>
        value.ToString("MMM d", CultureInfo.InvariantCulture);

    /// <summary>Formats a date as "Jun 30, 2026" (wireframe cert-row style).</summary>
    public static string LongDate(DateTime value) =>
        value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
}
