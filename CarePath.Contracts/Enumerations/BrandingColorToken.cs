namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.BrandingColorToken</c>. Values map to
/// design-system CSS custom properties; clients never receive raw color values.
/// </summary>
public enum BrandingColorToken
{
    /// <summary>Primary sidebar/brand teal (<c>--teal-900</c>).</summary>
    Teal900 = 1,

    /// <summary>Mid teal (<c>--teal-700</c>).</summary>
    Teal700 = 2,

    /// <summary>Light teal tint (<c>--teal-100</c>).</summary>
    Teal100 = 3,

    /// <summary>Focus/accent orange (<c>--focus</c>).</summary>
    Orange = 4,

    /// <summary>Soft status green.</summary>
    Green = 5,

    /// <summary>Soft status amber.</summary>
    Amber = 6,

    /// <summary>Soft status red.</summary>
    Red = 7
}
