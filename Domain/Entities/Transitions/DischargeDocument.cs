using CarePath.Domain.Entities.Common;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Transitions;

/// <summary>
/// Represents a hospital discharge document uploaded or imported for a client.
/// Holds the raw source content, extraction status, and links to the resulting
/// <see cref="TransitionPlan"/> once clinical review is complete.
/// </summary>
/// <remarks>
/// <b>PHI:</b> <see cref="RawContent"/> contains the full extracted text or FHIR JSON
/// payload from the discharge document. This field must never appear in logs,
/// exception messages, or URLs.
/// </remarks>
public class DischargeDocument : BaseEntity
{
    /// <summary>Foreign key to the client this discharge document belongs to.</summary>
    public Guid ClientId { get; set; }

    /// <summary>Navigation to the associated <see cref="Client"/>.</summary>
    public Client? Client { get; set; }

    /// <summary>How the document was ingested into the system.</summary>
    public DischargeDocumentSourceType SourceType { get; set; }

    /// <summary>
    /// Extracted raw text (PDF/photo OCR) or FHIR JSON payload.
    /// <b>PHI — never log this field.</b>
    /// </summary>
    public string? RawContent { get; set; }

    /// <summary>
    /// Original filename for uploads, or the FHIR DocumentReference resource ID for imports.
    /// Used for audit trail and re-processing. Not PHI itself.
    /// </summary>
    public string? SourceReference { get; set; }

    /// <summary>Current AI extraction and review processing state.</summary>
    public DischargeDocumentStatus Status { get; set; } = DischargeDocumentStatus.Pending;

    /// <summary>
    /// The <see cref="BaseEntity.Id"/> of the <see cref="User"/> who uploaded this document.
    /// Used in the HIPAA audit trail.
    /// </summary>
    public Guid UploadedBy { get; set; }

    /// <summary>UTC timestamp when the document was received by the system.</summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Transition plans generated from this document. Typically one per discharge episode.</summary>
    public IReadOnlyList<TransitionPlan> TransitionPlans { get; private set; } = new List<TransitionPlan>();
}
