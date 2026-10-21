using System.Text.RegularExpressions;

namespace CarePath.Application.Platform;

/// <summary>
/// Slug rules shared by validators and bootstrap: 3–40 characters, lower-case letters, digits,
/// single hyphens, no leading/trailing hyphen, and not a reserved word.
/// </summary>
public static partial class OrganizationSlug
{
    /// <summary>Slugs that can never be registered because they name platform surfaces.</summary>
    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>(StringComparer.Ordinal)
    {
        "platform",
        "admin",
        "api",
        "www",
        "login"
    };

    /// <summary>True when <paramref name="slug"/> is well-formed and not reserved.</summary>
    /// <param name="slug">Candidate slug.</param>
    public static bool IsValid(string? slug) =>
        !string.IsNullOrEmpty(slug)
        && SlugPattern().IsMatch(slug)
        && !slug.Contains("--", StringComparison.Ordinal)
        && !Reserved.Contains(slug);

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{1,38}[a-z0-9])$")]
    private static partial Regex SlugPattern();
}
