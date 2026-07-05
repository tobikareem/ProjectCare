namespace CarePath.Contracts.Scheduling;

/// <summary>
/// Visit photo metadata (D-S4-7). Clinical PHI-adjacent: reads are audit logged. Never carries
/// bytes, filesystem paths, or blob keys; <see cref="Url"/> stays null until the short-lived
/// signed-URL service exists (D-S4-3).
/// </summary>
public class VisitPhotoDto
{
    /// <summary>Photo identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Visit note the photo belongs to.</summary>
    public Guid VisitNoteId { get; init; }

    /// <summary>When the photo was taken (UTC).</summary>
    public DateTime TakenAt { get; init; }

    /// <summary>Optional caption. May contain PHI — never log.</summary>
    public string? Caption { get; init; }

    /// <summary>Short-lived, access-controlled read URL. Null until the signed-URL service exists.</summary>
    public string? Url { get; init; }
}
