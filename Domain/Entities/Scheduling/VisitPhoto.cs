using CarePath.Domain.Entities.Common;

namespace CarePath.Domain.Entities.Scheduling;

/// <summary>
/// A photo attached to a <see cref="VisitNote"/> and stored as a URL pointing to external blob storage.
/// Photos may document wound care, environmental hazards, client condition, or care activities.
/// </summary>
/// <remarks>
/// <b>Storage:</b> The photo file itself is stored in an external blob store (e.g., Azure Blob Storage).
/// Only the public or pre-signed URL is persisted here. The Infrastructure layer is responsible for
/// uploading and managing the blob lifecycle.
/// </remarks>
public class VisitPhoto : BaseEntity
{
    // Foreign Keys and Navigation

    /// <summary>Foreign key to the <see cref="VisitNote"/> this photo is attached to.</summary>
    public Guid VisitNoteId { get; set; }

    /// <summary>Navigation to the parent <see cref="VisitNote"/>. Required.</summary>
    public VisitNote VisitNote { get; set; } = null!;

    /// <summary>
    /// URL of the photo in external blob storage.
    /// The URL should be a durable, stable reference (not a short-lived SAS token stored here).
    /// </summary>
    public string PhotoUrl { get; set; } = string.Empty;

    /// <summary>Optional caption describing the photo (e.g., "Wound site — left heel, Day 3").</summary>
    public string? Caption { get; set; }

    /// <summary>UTC timestamp when the photo was taken on the device. Defaults to upload time.</summary>
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;
}
