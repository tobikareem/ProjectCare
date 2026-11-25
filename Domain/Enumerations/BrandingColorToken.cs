namespace CarePath.Domain.Enumerations;

/// <summary>
/// Named design-system color tokens an organization may choose for white-label branding.
/// Values map to CSS custom properties in the shared UI stylesheet; raw colors are never stored.
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
