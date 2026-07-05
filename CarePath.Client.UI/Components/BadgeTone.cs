namespace CarePath.Client.UI.Components;

/// <summary>
/// Visual tone for <c>StatusBadge</c>. Consuming apps style the corresponding CSS classes
/// (<c>cp-badge-neutral</c>, <c>cp-badge-info</c>, <c>cp-badge-success</c>,
/// <c>cp-badge-warning</c>, <c>cp-badge-danger</c>).
/// </summary>
public enum BadgeTone
{
    /// <summary>Default/neutral state.</summary>
    Neutral = 0,

    /// <summary>Informational state (e.g., scheduled, sent).</summary>
    Info = 1,

    /// <summary>Positive state (e.g., completed, paid).</summary>
    Success = 2,

    /// <summary>Attention state (e.g., in progress, partially paid).</summary>
    Warning = 3,

    /// <summary>Problem state (e.g., no-show, overdue).</summary>
    Danger = 4
}
