using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Transitions;

/// <summary>
/// A single instruction extracted from a discharge document by the AI extraction service.
/// Each instruction carries a confidence score and a link to its original source text
/// to support clinical review and audit.
/// </summary>
/// <remarks>
/// <b>PHI:</b> <see cref="SourceText"/> contains a verbatim excerpt from the patient's
/// discharge document. This field must never appear in logs or exception messages.
/// </remarks>
public class TransitionInstruction : BaseEntity
{
    /// <summary>Foreign key to the plan this instruction belongs to.</summary>
    public Guid TransitionPlanId { get; set; }

    /// <summary>Navigation to the parent <see cref="TransitionPlan"/>.</summary>
    public TransitionPlan? TransitionPlan { get; set; }

    /// <summary>Clinical category used to route this instruction to the correct reminder type.</summary>
    public TransitionInstructionCategory Category { get; set; }

    /// <summary>
    /// Plain-language version of the instruction shown to the patient and used as the reminder text.
    /// Edited by the clinician during review if the AI extraction was inaccurate.
    /// </summary>
    public string InstructionText { get; set; } = string.Empty;

    /// <summary>
    /// The verbatim text from the discharge document that this instruction was extracted from.
    /// Preserved to allow the clinician to verify the AI's interpretation against the source.
    /// <b>PHI — never log this field.</b>
    /// </summary>
    public string? SourceText { get; set; }

    /// <summary>
    /// AI extraction confidence score in the range 0.0–1.0.
    /// Values below 0.75 are considered low confidence and require mandatory clinician review.
    /// </summary>
    public decimal ConfidenceScore { get; set; }

    /// <summary>Clinician note added during review — rationale for edits, rejections, or pharmacist referrals.</summary>
    public string? ClinicalNote { get; set; }

    /// <summary>
    /// When <c>true</c>, the reviewing clinician should refer this instruction to a pharmacist
    /// before approving (e.g. complex polypharmacy, high-risk medication).
    /// </summary>
    public bool NeedsPharmacistReview { get; set; }

    /// <summary>Review and approval status within the clinician workflow.</summary>
    public TransitionInstructionStatus Status { get; set; } = TransitionInstructionStatus.Pending;

    // ── Computed properties (pure C# — no EF involvement) ──────────────────────

    /// <summary>
    /// Returns <c>true</c> when <see cref="ConfidenceScore"/> is below 0.75,
    /// indicating mandatory clinician review before activation.
    /// </summary>
    public bool IsLowConfidence => ConfidenceScore < 0.75m;
}
