using CarePath.Contracts.Enumerations;

namespace CarePath.Client.UI.Theming;

/// <summary>
/// Maps organization branding color tokens to the design-system CSS custom properties defined in
/// <c>carepath-ui.css</c> (CP-04 FR-012). Components apply the returned <c>var(--…)</c> references
/// only; raw color values never appear in markup or code (UI design rule: no hard-coded colors).
/// </summary>
public static class BrandingTokens
{
    /// <summary>The CSS custom property reference for a color token.</summary>
    /// <param name="token">The branding color token.</param>
    /// <returns>A <c>var(--name)</c> expression; unknown tokens fall back to the brand teal.</returns>
    public static string CssVar(BrandingColorToken token) => token switch
    {
        BrandingColorToken.Teal900 => "var(--teal-900)",
        BrandingColorToken.Teal700 => "var(--teal-700)",
        BrandingColorToken.Teal100 => "var(--teal-100)",
        BrandingColorToken.Orange => "var(--orange)",
        BrandingColorToken.Green => "var(--green)",
        BrandingColorToken.Amber => "var(--amber)",
        BrandingColorToken.Red => "var(--red)",
        _ => "var(--teal-900)",
    };

    /// <summary>
    /// The CSS custom property reference for a token used as a solid background under
    /// <c>var(--surface)</c> text. Light tints cannot carry white text (WCAG contrast), so
    /// <see cref="BrandingColorToken.Teal100"/> maps to the mid teal instead.
    /// </summary>
    /// <param name="token">The branding color token.</param>
    /// <returns>A <c>var(--name)</c> expression that is readable under surface-colored text.</returns>
    public static string ContrastSafeCssVar(BrandingColorToken token) =>
        token == BrandingColorToken.Teal100 ? CssVar(BrandingColorToken.Teal700) : CssVar(token);

    /// <summary>
    /// Inline style declaring the brand custom properties an organization-themed surface reads
    /// (<c>--brand-primary</c> for solid marks and buttons, <c>--brand-accent</c> for focus rings).
    /// </summary>
    /// <param name="primary">Primary color token.</param>
    /// <param name="accent">Accent color token.</param>
    /// <returns>A style attribute value.</returns>
    public static string BrandStyle(BrandingColorToken primary, BrandingColorToken accent) =>
        $"--brand-primary: {ContrastSafeCssVar(primary)}; --brand-accent: {CssVar(accent)};";

    /// <summary>
    /// Two-letter monogram for the brand mark: the initials of the first two words, or the first
    /// two letters of a single word. ASCII letters and digits only, by design: the mark is a
    /// fixed-width glyph box and non-ASCII initials render inconsistently across platforms.
    /// </summary>
    /// <param name="displayName">Organization display name.</param>
    /// <returns>The monogram, or <c>"CP"</c> when the name yields none.</returns>
    public static string Monogram(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "CP";
        }

        var words = displayName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => new string(word.Where(char.IsAsciiLetterOrDigit).ToArray()))
            .Where(word => word.Length > 0)
            .ToArray();

        var letters = words.Length switch
        {
            0 => string.Empty,
            1 => words[0][..Math.Min(2, words[0].Length)],
            _ => $"{words[0][0]}{words[1][0]}",
        };

        return letters.Length == 0 ? "CP" : letters.ToUpperInvariant();
    }
}
